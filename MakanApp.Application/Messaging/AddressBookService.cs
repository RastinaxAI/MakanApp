using MakanApp.Application.Commerce;

namespace MakanApp.Application.Messaging;

public static class AddressBookErrorCodes
{
    public const string Invalid = "ADDRESS_BOOK_INVALID";
    public const string ConsentRequired = "ADDRESS_BOOK_CONSENT_REQUIRED";
    public const string Conflict = "ADDRESS_BOOK_CONFLICT";
    public const string StaleSource = "ADDRESS_BOOK_SOURCE_STALE";
    public const string CursorInvalid = "ADDRESS_BOOK_CURSOR_INVALID";
}
public sealed record AddressBookActor(Guid UserId, Guid SessionId);
public sealed record SetContactConsentCommand(Guid DeviceId, string ConsentVersion, string Scope, bool SyncEnabled);
public sealed record ContactDeviceResult(Guid DeviceId, string ConsentVersion, string Scope, bool SyncEnabled, DateTime GrantedAtUtc, long LastSourceRevision);
public enum ContactMutationKind { Upsert = 1, Delete = 2, LinkAccount = 3 }
public enum ContactNameOrigin { Device = 1, Manual = 2 }
public sealed record ContactMutation(Guid EntryId, ContactMutationKind Kind, long? ExpectedServerVersion,
    string? SavedName = null, IReadOnlyCollection<string>? Phones = null, ContactNameOrigin NameOrigin = ContactNameOrigin.Device,
    Guid? LinkedAccountUserId = null);
public sealed record ContactSyncCommand(Guid DeviceId, Guid ClientOperationId, long SourceRevision, IReadOnlyCollection<ContactMutation> Entries);
public sealed record ContactSyncItemResult(Guid EntryId, string Status, long? ServerVersion, string? ErrorCode);
public sealed record ContactSyncResult(IReadOnlyCollection<ContactSyncItemResult> Items, string NextCursor, long ServerVersion);
public sealed record AddressBookEntryResult(Guid EntryId, string? SavedName, IReadOnlyCollection<string> Phones,
    Guid? LinkedAccountUserId, bool LinkCurrentlyVisible, long ServerVersion, DateTime UpdatedAtUtc, bool Deleted);
public sealed record ContactDeltaResult(IReadOnlyCollection<AddressBookEntryResult> Upserts, IReadOnlyCollection<AddressBookEntryResult> Tombstones,
    string NextCursor, long ServerVersion, bool HasMore);

public interface IAddressBookStore
{
    Task<ContactDeviceResult> ConsentAsync(AddressBookActor actor, SetContactConsentCommand command, DateTime now, CancellationToken ct);
    Task<ContactSyncResult> SyncAsync(AddressBookActor actor, ContactSyncCommand command, DateTime now, CancellationToken ct);
    Task<ContactDeltaResult> DeltaAsync(AddressBookActor actor, Guid deviceId, string? cursor, int limit, CancellationToken ct);
}

public sealed class AddressBookService(IAddressBookStore store, CommercialActorAccess actors, TimeProvider clock)
{
    public async Task<ContactDeviceResult> ConsentAsync(AddressBookActor actor, SetContactConsentCommand command, CancellationToken ct)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        if (command.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(command.ConsentVersion) || command.ConsentVersion.Length > 64 ||
            string.IsNullOrWhiteSpace(command.Scope) || command.Scope.Length > 64) throw Invalid();
        return await store.ConsentAsync(actor, command, clock.GetUtcNow().UtcDateTime, ct);
    }
    public async Task<ContactSyncResult> SyncAsync(AddressBookActor actor, ContactSyncCommand command, CancellationToken ct)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        if (command.DeviceId == Guid.Empty || command.ClientOperationId == Guid.Empty || command.SourceRevision < 1 ||
            command.Entries is null || command.Entries.Count is < 1 or > 100 || command.Entries.Any(e => e is null)) throw Invalid();
        return await store.SyncAsync(actor, command, clock.GetUtcNow().UtcDateTime, ct);
    }
    public async Task<ContactDeltaResult> DeltaAsync(AddressBookActor actor, Guid deviceId, string? cursor, int limit, CancellationToken ct)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        if (deviceId == Guid.Empty || limit is < 1 or > 200 || cursor?.Length > 4096) throw Invalid();
        return await store.DeltaAsync(actor, deviceId, cursor, limit, ct);
    }
    internal static MessagingException Invalid() => new(AddressBookErrorCodes.Invalid, "ورودی دفترچهٔ مخاطبان معتبر نیست.");
}
