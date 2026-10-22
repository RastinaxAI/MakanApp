using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MakanApp.Api.Configuration;
using MakanApp.Api.Hosting;
using MakanApp.Application.Platform;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OtpNet;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed class ProductionDataProtectionTests
{
    [Fact]
    public async Task LegacyAndCanonicalConfigurationPreserveExistingMfaPayloadAcrossProviders()
    {
        var settings = CertificateSettings();
        var userId = Guid.NewGuid();
        string secret;
        string cipher;
        using (var first = BuildServices(settings))
        {
            await StartAsync(first);
            Assert.Equal("MakanApp.BackofficeMfa.v1", first.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
            var crypto = first.GetRequiredService<IPlatformMfaCryptography>();
            secret = crypto.GenerateSecret();
            cipher = crypto.Protect(userId, secret);
            var existingPurpose = first.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("MakanApp.PlatformMfa.v1." + userId.ToString("N"));
            Assert.Equal(secret, Encoding.UTF8.GetString(existingPurpose.Unprotect(Convert.FromBase64String(cipher))));
        }

        var canonical = settings.ToDictionary(pair => pair.Key.Replace("BackofficeSecurity:", "Security:DataProtection:", StringComparison.Ordinal), pair => pair.Value);
        using var second = BuildServices(canonical);
        await StartAsync(second);
        var now = DateTime.UtcNow;
        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(now);
        Assert.True(second.GetRequiredService<IPlatformMfaCryptography>().Verify(userId, cipher, code, now, out _));
        Assert.True(await second.GetRequiredService<DataProtectionKeyRingProbe>().IsReadyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DifferentApplicationNamesCannotUnprotectPayloadFromTheSamePersistentRing()
    {
        var settings = CertificateSettings();
        byte[] payload;
        using (var first = BuildServices(settings))
        {
            await StartAsync(first);
            payload = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("makan-ph1-test-purpose").Protect([1, 2, 3]);
        }

        settings["Security:DataProtection:ApplicationName"] = "MakanApp.SeparateApplication.v1";
        using var second = BuildServices(settings);
        await StartAsync(second);
        Assert.Throws<CryptographicException>(() => second.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("makan-ph1-test-purpose").Unprotect(payload));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task ProductionLikeHostCannotStartWithAnEphemeralRing(string environment)
    {
        using var services = BuildServices([], environment);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(services));
        Assert.Contains("Data Protection", exception.Message, StringComparison.Ordinal);
        Assert.False(await services.GetRequiredService<DataProtectionKeyRingProbe>().IsReadyAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task DevelopmentAndTestingDoNotRequireAnOperationalKeyRing(string environment)
    {
        using var services = BuildServices([], environment);
        await StartAsync(services);
        Assert.True(await services.GetRequiredService<DataProtectionKeyRingProbe>().IsReadyAsync(CancellationToken.None));
    }

    [Fact]
    public void DirectMfaResolutionWithoutRingStillFailsClosed()
    {
        using var services = BuildServices([]);
        var crypto = services.GetRequiredService<IPlatformMfaCryptography>();
        var exception = Assert.Throws<PlatformException>(() => crypto.Protect(Guid.NewGuid(), crypto.GenerateSecret()));
        Assert.Equal(PlatformErrorCodes.MfaUnavailable, exception.Code);
    }

    [Fact]
    public async Task ReadinessDoesNotRecreateAMissingConfiguredDirectory()
    {
        var settings = CertificateSettings();
        var ring = settings["BackofficeSecurity:KeyRingPath"]!;
        using var services = BuildServices(settings);
        var probe = services.GetRequiredService<DataProtectionKeyRingProbe>();
        Assert.False(await probe.IsReadyAsync(CancellationToken.None));
        Assert.False(Directory.Exists(ring));
        await StartAsync(services);
        var retained = ring + "-retained";
        Directory.Move(ring, retained);
        Assert.False(await probe.IsReadyAsync(CancellationToken.None));
        Assert.False(Directory.Exists(ring));
        Assert.True(Directory.Exists(retained));
    }

    [Fact]
    public async Task ReadinessDetectsMissingPersistedKeysDespiteCachedProvider()
    {
        var settings = CertificateSettings();
        var ring = settings["BackofficeSecurity:KeyRingPath"]!;
        using var services = BuildServices(settings);
        await StartAsync(services);
        var files = Directory.GetFiles(ring, "key-*.xml");
        Assert.NotEmpty(files);
        foreach (var file in files) File.Move(file, file + ".retained");
        Assert.False(await services.GetRequiredService<DataProtectionKeyRingProbe>().IsReadyAsync(CancellationToken.None));
        Assert.Empty(Directory.GetFiles(ring, "key-*.xml"));
    }

    [Fact]
    public async Task AnotherPersistedKeyDoesNotHideLossOfTheCachedProvidersOriginalKey()
    {
        var settings = CertificateSettings();
        var ring = settings["BackofficeSecurity:KeyRingPath"]!;
        using var services = BuildServices(settings);
        await StartAsync(services);
        var original = Assert.Single(Directory.GetFiles(ring, "key-*.xml"));
        services.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(90));
        Assert.Equal(2, Directory.GetFiles(ring, "key-*.xml").Length);
        File.Move(original, original + ".retained");
        Assert.False(await services.GetRequiredService<DataProtectionKeyRingProbe>().IsReadyAsync(CancellationToken.None));
        Assert.Single(Directory.GetFiles(ring, "key-*.xml"));
    }

    [Fact]
    public async Task MalformedPersistedDescriptorCannotBeIgnoredAndReseededAtStartup()
    {
        var settings = CertificateSettings();
        var ring = settings["BackofficeSecurity:KeyRingPath"]!;
        Directory.CreateDirectory(ring);
        var corruptFile = Path.Combine(ring, "key-" + Guid.NewGuid().ToString("D") + ".xml");
        const string marker = "<key id=\"invalid-synthetic-marker\" />";
        await File.WriteAllTextAsync(corruptFile, marker);
        using var services = BuildServices(settings);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(services));
        Assert.DoesNotContain(corruptFile, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(marker, exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(marker, await File.ReadAllTextAsync(corruptFile));
        Assert.Single(Directory.GetFiles(ring, "key-*.xml"));
    }

    [Fact]
    public async Task RuntimeRemembersNewlyObservedRotationKeysWithoutCreatingReplacementKeys()
    {
        var settings = CertificateSettings();
        var ring = settings["BackofficeSecurity:KeyRingPath"]!;
        using var services = BuildServices(settings);
        await StartAsync(services);
        var rotated = services.GetRequiredService<IKeyManager>()
            .CreateNewKey(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(90));
        var probe = services.GetRequiredService<DataProtectionKeyRingProbe>();
        Assert.True(await probe.IsReadyAsync(CancellationToken.None));
        var rotatedFile = Path.Combine(ring, "key-" + rotated.KeyId.ToString("D") + ".xml");
        File.Move(rotatedFile, rotatedFile + ".retained");
        Assert.False(await probe.IsReadyAsync(CancellationToken.None));
        Assert.Single(Directory.GetFiles(ring, "key-*.xml"));
    }

    [Fact]
    public async Task FileInsteadOfDirectoryFailsBeforeHostStartupWithoutLeakingPath()
    {
        var settings = CertificateSettings();
        var markerPath = Path.Combine(Path.GetDirectoryName(settings["BackofficeSecurity:CertificatePath"])!, "private-path-marker.txt");
        await File.WriteAllTextAsync(markerPath, "synthetic test marker");
        settings["BackofficeSecurity:KeyRingPath"] = markerPath;
        using var services = BuildServices(settings);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(services));
        Assert.DoesNotContain(markerPath, exception.ToString(), StringComparison.Ordinal);
        Assert.Equal("synthetic test marker", await File.ReadAllTextAsync(markerPath));
    }

    [Theory]
    [InlineData("Security:DataProtection:KeyRingPath", "relative-path")]
    [InlineData("Security:DataProtection:CertificatePath", "relative-certificate.pfx")]
    [InlineData("Security:DataProtection:ApplicationName", "")]
    [InlineData("Security:DataProtection:ApplicationName", "  unstable-name  ")]
    [InlineData("Security:DataProtection:ApplicationName", "unsafe\nname")]
    public void InvalidExplicitConfigurationIsRejected(string key, string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BuildServices(new Dictionary<string, string?> { [key] = value }));
        Assert.DoesNotContain(value.Length == 0 ? "secret-marker" : value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SameNormalizedAliasPathsAreAccepted()
    {
        var root = Path.Combine(Path.GetTempPath(), "MakanApp-PH1-Aliases-" + Guid.NewGuid().ToString("N"));
        using var services = BuildServices(new Dictionary<string, string?>
        {
            ["BackofficeSecurity:KeyRingPath"] = root,
            ["Security:DataProtection:KeyRingPath"] = root + Path.DirectorySeparatorChar
        });
        Assert.Equal(root, services.GetRequiredService<DataProtectionKeyRingSettings>().KeyRingPath);
    }

    [Theory]
    [InlineData("KeyRingPath")]
    [InlineData("CertificatePath")]
    [InlineData("CertificatePassword")]
    public void ConflictingLegacyAndCanonicalAliasesAreRejectedWithoutValues(string name)
    {
        var first = name.EndsWith("Path", StringComparison.Ordinal)
            ? Path.Combine(Path.GetTempPath(), "private-first-marker") : "first-secret-marker";
        var second = name.EndsWith("Path", StringComparison.Ordinal)
            ? Path.Combine(Path.GetTempPath(), "private-second-marker") : "second-secret-marker";
        var exception = Assert.Throws<InvalidOperationException>(() => BuildServices(new Dictionary<string, string?>
        {
            [$"BackofficeSecurity:{name}"] = first,
            [$"Security:DataProtection:{name}"] = second
        }));
        Assert.DoesNotContain(first, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(second, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalRingCannotOverlapPublishOrPublicStorage()
    {
        var root = Path.Combine(Path.GetTempPath(), "MakanApp-PH1-Overlap-" + Guid.NewGuid().ToString("N"));
        var environment = new TestEnvironment { ContentRootPath = Path.Combine(root, "publish") };
        Assert.Throws<InvalidOperationException>(() => BuildServices(new Dictionary<string, string?>
        {
            ["Security:DataProtection:KeyRingPath"] = Path.Combine(environment.ContentRootPath, "keys")
        }, hostEnvironment: environment));
        Assert.Throws<InvalidOperationException>(() => BuildServices(new Dictionary<string, string?>
        {
            ["Storage:RootPath"] = Path.Combine(root, "storage"),
            ["Security:DataProtection:KeyRingPath"] = Path.Combine(root, "storage", "keys")
        }, hostEnvironment: environment));
    }

    [Fact]
    public void CertificateLoadFailureDoesNotExposePathOrPassword()
    {
        var root = Path.Combine(Path.GetTempPath(), "MakanApp-PH1-CertificateFailure-" + Guid.NewGuid().ToString("N"));
        var certificatePath = Path.Combine(root, "private-certificate-marker.pfx");
        var exception = Assert.Throws<InvalidOperationException>(() => BuildServices(new Dictionary<string, string?>
        {
            ["Security:DataProtection:KeyRingPath"] = Path.Combine(root, "keys"),
            ["Security:DataProtection:CertificatePath"] = certificatePath,
            ["Security:DataProtection:CertificatePassword"] = "secret-password-marker"
        }));
        Assert.DoesNotContain(certificatePath, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-password-marker", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CertificateWithoutPrivateKeyIsRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "MakanApp-PH1-PublicCertificate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=MakanApp-PH1-Public-Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var certificatePath = Path.Combine(root, "public-test-only.pfx");
        using var publicCertificate = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
        File.WriteAllBytes(certificatePath, publicCertificate.Export(X509ContentType.Pfx));
        var exception = Assert.Throws<InvalidOperationException>(() => BuildServices(new Dictionary<string, string?>
        {
            ["Security:DataProtection:KeyRingPath"] = Path.Combine(root, "keys"),
            ["Security:DataProtection:CertificatePath"] = certificatePath
        }));
        Assert.DoesNotContain(certificatePath, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelledProbeDoesNotCreateDirectory()
    {
        var settings = CertificateSettings();
        using var services = BuildServices(settings);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => services.GetRequiredService<DataProtectionKeyRingProbe>()
            .InitializeAsync(cancellation.Token));
        Assert.False(Directory.Exists(settings["BackofficeSecurity:KeyRingPath"]));
    }

    private static Task StartAsync(ServiceProvider services) =>
        services.GetRequiredService<IHostedService>().StartAsync(CancellationToken.None);

    private static ServiceProvider BuildServices(Dictionary<string, string?> settings, string environment = "Production", TestEnvironment? hostEnvironment = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackofficeMfa(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            hostEnvironment ?? new TestEnvironment { EnvironmentName = environment });
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> CertificateSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "MakanApp-PH1-Keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var certificatePath = Path.Combine(root, "synthetic-test-only.pfx");
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=MakanApp-PH1-Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, password));
        return new Dictionary<string, string?>
        {
            ["BackofficeSecurity:KeyRingPath"] = Path.Combine(root, "keys"),
            ["BackofficeSecurity:CertificatePath"] = certificatePath,
            ["BackofficeSecurity:CertificatePassword"] = password
        };
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "MakanApp.PH1.Tests";
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "MakanApp-PH1-Publish-" + Guid.NewGuid().ToString("N"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
