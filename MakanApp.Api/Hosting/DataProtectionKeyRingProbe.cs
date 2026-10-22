using System.Security;
using System.Security.Cryptography;
using System.Xml;
using MakanApp.Api.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace MakanApp.Api.Hosting;

public sealed class DataProtectionKeyRingProbe(
    DataProtectionKeyRingSettings settings,
    IDataProtectionProvider provider,
    IKeyManager keyManager) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly byte[] _probeValue = RandomNumberGenerator.GetBytes(32);
    private byte[]? _protectedProbe;
    private HashSet<Guid>? _persistedKeyIds;

    public bool PersistenceRequired => settings.PersistenceRequired;
    public bool IsConfigured => settings.KeyRingPath is not null;

    public Task<bool> InitializeAsync(CancellationToken cancellationToken) => CheckAsync(true, cancellationToken);
    public Task<bool> IsReadyAsync(CancellationToken cancellationToken) => CheckAsync(false, cancellationToken);

    private async Task<bool> CheckAsync(bool initialize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (settings.KeyRingPath is null) return !settings.PersistenceRequired;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (initialize) Directory.CreateDirectory(settings.KeyRingPath);
            // readiness نباید حلقهٔ حذف‌شده را دوباره بسازد و گم‌شدن کلیدهای قبلی را پنهان کند.
            if (!Directory.Exists(settings.KeyRingPath)) return false;
            var keys = keyManager.GetAllKeys();
            // فایل‌های key-*.xml خروجی استاندارد provider هستند؛ descriptor نادیده‌گرفته‌شده نباید reseed را مجاز کند.
            if (Directory.EnumerateFiles(settings.KeyRingPath, "key-*.xml").Count() != keys.Count) return false;
            var availableIds = keys.Where(key => !key.IsRevoked).Select(key => key.KeyId).ToHashSet();
            if (!initialize && (_protectedProbe is null || availableIds.Count == 0)) return false;
            // provider کلیدها را cache می‌کند؛ ماندن یک XML دیگر، حذف کلید استفاده‌شده را جبران نمی‌کند.
            if (_persistedKeyIds is not null && !_persistedKeyIds.IsSubsetOf(availableIds)) return false;
            foreach (var key in keys.Where(key => !key.IsRevoked))
            {
                _ = key.CreateEncryptor();
            }

            var probePath = Path.Combine(settings.KeyRingPath, ".makan-keyring-probe-" + Guid.NewGuid().ToString("N"));
            await using (var file = new FileStream(probePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                             4096, FileOptions.Asynchronous | FileOptions.DeleteOnClose))
            {
                await file.WriteAsync(_probeValue, cancellationToken);
                await file.FlushAsync(cancellationToken);
                file.Position = 0;
                var read = new byte[_probeValue.Length];
                await file.ReadExactlyAsync(read, cancellationToken);
                if (!CryptographicOperations.FixedTimeEquals(read, _probeValue)) return false;
            }

            var protector = provider.CreateProtector("MakanApp.Operations.KeyRingProbe.v1");
            _protectedProbe ??= protector.Protect(_probeValue);
            var persistedKeys = keyManager.GetAllKeys();
            if (Directory.EnumerateFiles(settings.KeyRingPath, "key-*.xml").Count() != persistedKeys.Count) return false;
            var persistedIds = persistedKeys.Where(key => !key.IsRevoked).Select(key => key.KeyId).ToHashSet();
            if (persistedIds.Count == 0 || !CryptographicOperations.FixedTimeEquals(protector.Unprotect(_protectedProbe), _probeValue))
                return false;
            _persistedKeyIds ??= persistedIds;
            _persistedKeyIds.UnionWith(persistedIds);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                             CryptographicException or SecurityException or InvalidOperationException or
                                             ArgumentException or NotSupportedException or FormatException or XmlException)
        {
            return false;
        }
        finally { _gate.Release(); }
    }

    public void Dispose() => _gate.Dispose();
}
