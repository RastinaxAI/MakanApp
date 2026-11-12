using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed class EfAddressBookStore(MakanDbContext db, AddressBookProtection protection,
    ContactPhoneCanonicalizer phones, IMessagingDirectoryService directory, TimeProvider clock) : IAddressBookStore
{
    private sealed record Content(string? SavedName, string[] Phones);
    private sealed record Cursor(Guid Owner, Guid Device, Guid Consent, long Version);

    public async Task<ContactDeviceResult> ConsentAsync(AddressBookActor actor, SetContactConsentCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAccountAsync(actor, ct);
        var device = await db.ContactSyncDevices.SingleOrDefaultAsync(d => d.OwnerUserId == actor.UserId && d.DeviceId == command.DeviceId, ct);
        if (device is null) { device = ContactSyncDevice.Create(actor.UserId, command.DeviceId); db.ContactSyncDevices.Add(device); }
        if (device.ConsentVersion != command.ConsentVersion.Trim() || device.Scope != command.Scope.Trim() || device.SyncEnabled != command.SyncEnabled || device.ConsentId == Guid.Empty)
            device.Consent(command.ConsentVersion.Trim(), command.Scope.Trim(), command.SyncEnabled, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(device.DeviceId, device.ConsentVersion, device.Scope, device.SyncEnabled, device.GrantedAtUtc, device.LastSourceRevision);
    }

    public async Task<ContactSyncResult> SyncAsync(AddressBookActor actor, ContactSyncCommand command, DateTime now, CancellationToken ct)
    {
        var hash = protection.Fingerprint(actor.UserId, "operation", JsonSerializer.Serialize(command));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAccountAsync(actor, ct);
        var device = await RequireDeviceAsync(actor.UserId, command.DeviceId, ct);
        var replay = await db.ContactSyncOperations.AsNoTracking().SingleOrDefaultAsync(o => o.OwnerUserId == actor.UserId &&
            o.DeviceId == command.DeviceId && o.ClientOperationId == command.ClientOperationId, ct);
        if (replay is not null)
        {
            if (replay.RequestFingerprint != hash) throw Error(AddressBookErrorCodes.Conflict);
            var receipt = JsonSerializer.Deserialize<ContactSyncResult>(replay.Receipt)!;
            await tx.CommitAsync(ct); return receipt;
        }
        if (command.SourceRevision <= device.LastSourceRevision) throw Error(AddressBookErrorCodes.StaleSource);
        var state = await db.ContactSyncStates.SingleOrDefaultAsync(s => s.OwnerUserId == actor.UserId, ct);
        if (state is null) { state = ContactSyncState.Create(actor.UserId); db.ContactSyncStates.Add(state); }
        var results = new List<ContactSyncItemResult>();
        var seen = new HashSet<Guid>();
        foreach (var item in command.Entries)
        {
            if (!seen.Add(item.EntryId)) { results.Add(new(item.EntryId, "Rejected", null, AddressBookErrorCodes.Invalid)); continue; }
            results.Add(await ApplyAsync(actor, command, item, state, now, ct));
        }
        device.Advance(command.SourceRevision);
        // Cursor از نسخهٔ پس از Sync شروع می‌شود؛ برای بازسازی کل دفترچه، Delta بدون cursor خوانده می‌شود.
        var result = new ContactSyncResult(results, Encode(actor.UserId, device, state.ServerVersion), state.ServerVersion);
        db.ContactSyncOperations.Add(ContactSyncOperation.Create(actor.UserId, command.DeviceId, command.ClientOperationId,
            hash, JsonSerializer.Serialize(result), now));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }

    private async Task<ContactSyncItemResult> ApplyAsync(AddressBookActor actor, ContactSyncCommand command, ContactMutation item,
        ContactSyncState state, DateTime now, CancellationToken ct)
    {
        AddressBookEntry? entry = null;
        // پیوند فقط عملیات صریحِ خصوصی است؛ Sync شماره حق تطبیق خودکار حساب ندارد.
        if (item.Kind != ContactMutationKind.LinkAccount && item.LinkedAccountUserId.HasValue) return Reject(AddressBookErrorCodes.Invalid);
        if (item.EntryId == Guid.Empty || !Enum.IsDefined(item.Kind) || !Enum.IsDefined(item.NameOrigin) || item.ExpectedServerVersion < 0 ||
            item.SavedName?.Length > 200 || item.SavedName?.Any(c => char.IsControl(c)) == true || item.LinkedAccountUserId == Guid.Empty)
            return Reject(AddressBookErrorCodes.Invalid);
        entry = await db.AddressBookEntries.SingleOrDefaultAsync(e => e.OwnerUserId == actor.UserId && e.Id == item.EntryId, ct);
        if (entry?.DeletedAtUtc is not null) return new(item.EntryId, "Tombstone", entry.ServerVersion, AddressBookErrorCodes.Conflict);
        if (entry is null ? item.ExpectedServerVersion is not null and not 0 : item.ExpectedServerVersion != entry.ServerVersion)
            return Reject(AddressBookErrorCodes.Conflict);
        if (item.Kind != ContactMutationKind.Upsert && entry is null) return Reject(AddressBookErrorCodes.Conflict);
        Content? content = null;
        if (item.Kind == ContactMutationKind.LinkAccount)
        {
            if (item.Phones is not null || item.SavedName is not null) return Reject(AddressBookErrorCodes.Invalid);
            if (item.LinkedAccountUserId.HasValue)
                try { _ = await directory.GetPublicProfileAsync(actor.UserId, item.LinkedAccountUserId.Value, ct); }
                catch (MessagingException e) when (e.Code == MessagingErrorCodes.PersonNotDiscoverable) { return Reject(MessagingErrorCodes.PersonNotDiscoverable); }
            content = ReadContent(entry!);
        }
        if (item.Kind == ContactMutationKind.Upsert)
        {
            if (item.Phones is null || item.Phones.Count is < 1 or > 10 || item.Phones.Any(p => p is null)) return Reject(AddressBookErrorCodes.Invalid);
            string[] normalized;
            try { normalized = item.Phones.Select(phones.Normalize).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(); }
            catch (ArgumentException) { return Reject(AddressBookErrorCodes.Invalid); }
            var name = string.IsNullOrWhiteSpace(item.SavedName) ? null : item.SavedName.Trim();
            if (entry?.HasManualName == true && item.NameOrigin == ContactNameOrigin.Device) name = ReadContent(entry).SavedName;
            content = new(name, normalized);
        }
        var version = state.NextVersion();
        if (entry is null)
        {
            entry = AddressBookEntry.Create(actor.UserId, item.EntryId, protection.Protect(actor.UserId, item.EntryId.ToString("N"), JsonSerializer.Serialize(content)),
                item.NameOrigin == ContactNameOrigin.Manual, version, now, item.LinkedAccountUserId);
            db.AddressBookEntries.Add(entry);
        }
        else
        {
            if (item.Kind == ContactMutationKind.Delete) entry.Delete(version, now);
            else entry.Update(protection.Protect(actor.UserId, entry.Id.ToString("N"), JsonSerializer.Serialize(content)),
                entry.HasManualName || (item.Kind == ContactMutationKind.Upsert && item.NameOrigin == ContactNameOrigin.Manual),
                item.Kind == ContactMutationKind.LinkAccount ? item.LinkedAccountUserId : entry.LinkedAccountUserId, version, now);
        }
        var desiredIndexes = content?.Phones.Select(p => protection.Fingerprint(actor.UserId, "phone", p)).ToHashSet(StringComparer.Ordinal) ?? [];
        var oldPhones = await db.ContactPhones.Where(p => p.OwnerUserId == actor.UserId && p.EntryId == entry.Id).ToArrayAsync(ct);
        db.ContactPhones.RemoveRange(oldPhones.Where(p => !desiredIndexes.Contains(p.PhoneIndex)));
        foreach (var index in desiredIndexes.Except(oldPhones.Select(p => p.PhoneIndex))) db.ContactPhones.Add(ContactPhone.Create(actor.UserId, entry.Id, index));
        var binding = await db.ContactDeviceBindings.SingleOrDefaultAsync(b => b.OwnerUserId == actor.UserId && b.DeviceId == command.DeviceId && b.EntryId == entry.Id, ct);
        if (binding is null) { binding = ContactDeviceBinding.Create(actor.UserId, command.DeviceId, entry.Id); db.ContactDeviceBindings.Add(binding); }
        binding.Advance(command.SourceRevision, entry.ServerVersion);
        return new(item.EntryId, item.Kind == ContactMutationKind.Delete ? "Deleted" : "Upserted", entry.ServerVersion, null);
        ContactSyncItemResult Reject(string code) => new(item.EntryId, "Rejected", entry?.ServerVersion, code);
    }

    public async Task<ContactDeltaResult> DeltaAsync(AddressBookActor actor, Guid deviceId, string? cursor, int limit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAccountAsync(actor, ct);
        var device = await RequireDeviceAsync(actor.UserId, deviceId, ct);
        var version = 0L;
        if (cursor is not null)
        {
            try
            {
                var decoded = JsonSerializer.Deserialize<Cursor>(protection.Unprotect(actor.UserId, "cursor", cursor));
                if (decoded is null || decoded.Owner != actor.UserId || decoded.Device != deviceId || decoded.Consent != device.ConsentId || decoded.Version < 0)
                    throw Error(AddressBookErrorCodes.CursorInvalid);
                version = decoded.Version;
            }
            catch (Exception e) when (e is CryptographicException or JsonException or FormatException)
            { throw Error(AddressBookErrorCodes.CursorInvalid); }
        }
        var head = await db.ContactSyncStates.Where(s => s.OwnerUserId == actor.UserId).Select(s => (long?)s.ServerVersion).SingleOrDefaultAsync(ct) ?? 0;
        if (version > head) throw Error(AddressBookErrorCodes.CursorInvalid);
        var rows = await db.AddressBookEntries.AsNoTracking().Where(e => e.OwnerUserId == actor.UserId && e.ServerVersion > version)
            .OrderBy(e => e.ServerVersion).Take(limit + 1).ToArrayAsync(ct);
        var result = new List<AddressBookEntryResult>();
        foreach (var entry in rows.Take(limit))
        {
            if (entry.DeletedAtUtc.HasValue) { result.Add(new(entry.Id, null, [], null, false, entry.ServerVersion, entry.UpdatedAtUtc, true)); continue; }
            var content = ReadContent(entry); var linked = entry.LinkedAccountUserId;
            if (linked.HasValue)
                try { _ = await directory.GetPublicProfileAsync(actor.UserId, linked.Value, ct); }
                catch (MessagingException e) when (e.Code == MessagingErrorCodes.PersonNotDiscoverable) { linked = null; }
            result.Add(new(entry.Id, content.SavedName, content.Phones, linked, linked.HasValue, entry.ServerVersion, entry.UpdatedAtUtc, false));
        }
        var more = rows.Length > limit; var next = more ? rows[limit - 1].ServerVersion : head;
        await tx.CommitAsync(ct);
        return new(result.Where(r => !r.Deleted).ToArray(), result.Where(r => r.Deleted).ToArray(), Encode(actor.UserId, device, next), head, more);
    }
    private Content ReadContent(AddressBookEntry entry) => JsonSerializer.Deserialize<Content>(protection.Unprotect(entry.OwnerUserId, entry.Id.ToString("N"), entry.ProtectedContent))!;
    private string Encode(Guid owner, ContactSyncDevice device, long version) => protection.Protect(owner, "cursor", JsonSerializer.Serialize(new Cursor(owner, device.DeviceId, device.ConsentId, version)));
    private async Task<ContactSyncDevice> RequireDeviceAsync(Guid owner, Guid device, CancellationToken ct) =>
        await db.ContactSyncDevices.SingleOrDefaultAsync(d => d.OwnerUserId == owner && d.DeviceId == device && d.SyncEnabled, ct) ?? throw Error(AddressBookErrorCodes.ConsentRequired);
    private async Task LockAccountAsync(AddressBookActor actor, CancellationToken ct)
    {
        // قفل مشترک همهٔ دستگاه‌های یک حساب، ترتیب قطعی نسخه‌ها و نتیجهٔ هم‌زمانی را تأمین می‌کند.
        _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={actor.UserId}")
            .AsNoTracking().Select(u => u.Id).SingleOrDefaultAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (!await db.UserSessions.AsNoTracking().AnyAsync(s => s.Id == actor.SessionId && s.UserId == actor.UserId && s.RevokedAtUtc == null && s.ExpiresAtUtc > now && s.AbsoluteExpiresAtUtc > now &&
            db.Users.Any(u => u.Id == s.UserId && u.AccountStatus == UserAccountStatus.Active), ct))
            throw new IdentityException(IdentityErrorCodes.AuthRequired, "نشست معتبر لازم است.");
    }
    private static MessagingException Error(string code) => new(code, "همگام‌سازی دفترچهٔ مخاطبان قابل انجام نیست؛ وضعیت دستگاه و نسخه را بررسی کنید.");
}
