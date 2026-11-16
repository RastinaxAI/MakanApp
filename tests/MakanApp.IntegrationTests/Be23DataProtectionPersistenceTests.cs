using System.Security.Cryptography;
using MakanApp.Api.Realtime;
using MakanApp.Infrastructure.Messaging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed class Be23DataProtectionPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingProtectedStateSurvivesHostRestartOrRingRestoreButRejectsOtherBoundaries(bool restoreCopy)
    {
        var root = Path.Combine(Path.GetTempPath(), "MakanApp_Be23Protection_" + Guid.NewGuid().ToString("N"));
        var ring = Path.Combine(root, "original-ring");
        Directory.CreateDirectory(ring);
        var owner = Guid.NewGuid();
        var indexKey = RandomNumberGenerator.GetBytes(48);
        const string application = "MakanApp.Be23.ControlledDrill";
        const string cursorValue = "synthetic-account-replay-state";
        const string privateValue = "synthetic-private-address-book-value";
        string cursor;
        string contact;
        string fingerprint;

        using (var first = Host(ring, application))
        {
            await first.StartAsync();
            var provider = first.Services.GetRequiredService<IDataProtectionProvider>();
            var addressBook = new AddressBookProtection(provider, indexKey);
            cursor = new RealtimeCursorProtector(provider).Protect(cursorValue);
            contact = addressBook.Protect(owner, "name", privateValue);
            fingerprint = addressBook.Fingerprint(owner, "name", privateValue);
            await first.StopAsync();
        }

        var keys = Directory.GetFiles(ring, "*.xml");
        Assert.NotEmpty(keys);
        var savedKeys = keys.ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);
        if (restoreCopy)
        {
            var restoredRing = Path.Combine(root, "restored-ring");
            Directory.CreateDirectory(restoredRing);
            foreach (var key in keys)
                File.Copy(key, Path.Combine(restoredRing, Path.GetFileName(key)), overwrite: false);
            ring = restoredRing;
        }

        using (var restarted = Host(ring, application))
        {
            await restarted.StartAsync();
            var provider = restarted.Services.GetRequiredService<IDataProtectionProvider>();
            var addressBook = new AddressBookProtection(provider, indexKey);
            Assert.Equal(cursorValue, new RealtimeCursorProtector(provider).Unprotect(cursor));
            Assert.Equal(privateValue, addressBook.Unprotect(owner, "name", contact));
            Assert.Equal(fingerprint, addressBook.Fingerprint(owner, "name", privateValue));
            Assert.Throws<CryptographicException>(() => addressBook.Unprotect(Guid.NewGuid(), "name", contact));
            Assert.Throws<CryptographicException>(() => addressBook.Unprotect(owner, "phone", contact));
            await restarted.StopAsync();
        }

        foreach (var (name, bytes) in savedKeys)
            Assert.True(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(ring, name))), "بایت کلید Ring تغییر کرده است.");

        using (var wrongApplication = Host(ring, application + ".other"))
        {
            var provider = wrongApplication.Services.GetRequiredService<IDataProtectionProvider>();
            Assert.Throws<CryptographicException>(() => new RealtimeCursorProtector(provider).Unprotect(cursor));
            Assert.Throws<CryptographicException>(() => new AddressBookProtection(provider, indexKey).Unprotect(owner, "name", contact));
        }
        using (var wrongRing = Host(Path.Combine(root, "wrong-ring"), application))
        {
            var provider = wrongRing.Services.GetRequiredService<IDataProtectionProvider>();
            Assert.Throws<CryptographicException>(() => new RealtimeCursorProtector(provider).Unprotect(cursor));
            Assert.Throws<CryptographicException>(() => new AddressBookProtection(provider, indexKey).Unprotect(owner, "name", contact));
        }
        // فقط ring مصنوعی این تست حفظ می‌شود؛ هیچ ring واقعی یا تنظیم Production مصرف نمی‌شود.
    }

    private static IHost Host(string ring, string application) => new HostBuilder()
        .ConfigureLogging(logging => logging.ClearProviders())
        .ConfigureServices(services =>
        {
            services.AddDataProtection().SetApplicationName(application).PersistKeysToFileSystem(new DirectoryInfo(ring));
        })
        .Build();
}
