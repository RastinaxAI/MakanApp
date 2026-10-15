using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MakanApp.Api.Configuration;
using MakanApp.Application.Platform;
using MakanApp.Infrastructure.Platform;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OtpNet;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed class PlatformMfaProtectionConfigurationTests
{
    [Fact]
    public void PersistentKeyRingCanDecryptAfterProviderRecreation()
    {
        var directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "MakanApp-MfaTestKeys", Guid.NewGuid().ToString("N")));
        var user = Guid.NewGuid();
        var first = new TotpMfaCryptography(DataProtectionProvider.Create(directory));
        var secret = first.GenerateSecret();
        var cipher = first.Protect(user, secret);
        var now = DateTime.UtcNow;
        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(now);
        var second = new TotpMfaCryptography(DataProtectionProvider.Create(directory));
        Assert.True(second.Verify(user, cipher, code, now, out _));
        Assert.NotEqual(secret, cipher);
    }
    [Fact]
    public void ProductionWithoutExplicitPersistentKeyRingFailsClosedForTotp()
    {
        using var services = BuildServices(new Dictionary<string, string?>());
        var crypto = services.GetRequiredService<IPlatformMfaCryptography>();
        var error = Assert.Throws<PlatformException>(() => crypto.Protect(Guid.NewGuid(), crypto.GenerateSecret()));
        Assert.Equal(PlatformErrorCodes.MfaUnavailable, error.Code);
    }

    [Fact]
    public void ProductionKeyRingMustNotOverlapApplicationFileStorage()
    {
        var root = Path.Combine(Path.GetTempPath(), "MakanApp-MfaConfig-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidOperationException>(() => BuildServices(new Dictionary<string, string?>
        {
            ["Storage:RootPath"] = root,
            ["BackofficeSecurity:KeyRingPath"] = Path.Combine(root, "keys")
        }));
    }

    [Fact]
    public void CertificateEncryptedProductionKeyRingSurvivesCompositionRootRecreation()
    {
        var root = Path.Combine(Path.GetTempPath(), "MakanApp-MfaCertificate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var ring = Path.Combine(root, "keys");
        var certificatePath = Path.Combine(root, "test-only.pfx");
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=MakanApp-Mfa-Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, password));
        var settings = new Dictionary<string, string?>
        {
            ["BackofficeSecurity:KeyRingPath"] = ring,
            ["BackofficeSecurity:CertificatePath"] = certificatePath,
            ["BackofficeSecurity:CertificatePassword"] = password
        };
        var user = Guid.NewGuid();
        string secret;
        string protectedSecret;
        using (var first = BuildServices(settings))
        {
            var crypto = first.GetRequiredService<IPlatformMfaCryptography>();
            secret = crypto.GenerateSecret();
            protectedSecret = crypto.Protect(user, secret);
        }
        var keyXml = File.ReadAllText(Assert.Single(Directory.GetFiles(ring, "key-*.xml")));
        Assert.Contains("encryptedSecret", keyXml);
        Assert.DoesNotContain(secret, keyXml);
        var now = DateTime.UtcNow;
        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(now);
        using var second = BuildServices(settings);
        Assert.True(second.GetRequiredService<IPlatformMfaCryptography>().Verify(user, protectedSecret, code, now, out _));
    }

    private static ServiceProvider BuildServices(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackofficeMfa(configuration, new TestEnvironment());
        return services.BuildServiceProvider();
    }
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "MakanApp.Mfa.Tests";
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "MakanApp-Mfa-App-" + Guid.NewGuid().ToString("N"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
