namespace MakanApp.Domain.Messaging;

public sealed class AddressBookEntry
{
    private AddressBookEntry() { }
    public Guid OwnerUserId { get; private set; }
    public Guid Id { get; private set; }
    public string ProtectedContent { get; private set; } = "";
    public bool HasManualName { get; private set; }
    public Guid? LinkedAccountUserId { get; private set; }
    public long ServerVersion { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static AddressBookEntry Create(Guid owner, Guid id, string content, bool manual, long version, DateTime now, Guid? linkedAccount = null)
    {
        if (owner == Guid.Empty || id == Guid.Empty || version < 1 || string.IsNullOrEmpty(content) || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("ورودی مخاطب معتبر نیست.");
        return new() { OwnerUserId = owner, Id = id, ProtectedContent = content, HasManualName = manual,
            ServerVersion = version, CreatedAtUtc = now, UpdatedAtUtc = now, LinkedAccountUserId = linkedAccount };
    }
    public void Update(string content, bool manual, Guid? linkedAccount, long version, DateTime now)
    {
        EnsureMutable(version, now);
        if (string.IsNullOrEmpty(content) || linkedAccount == Guid.Empty) throw new ArgumentException("ورودی مخاطب معتبر نیست.");
        ProtectedContent = content; HasManualName = manual; LinkedAccountUserId = linkedAccount;
        ServerVersion = version; UpdatedAtUtc = now;
    }
    public void Delete(long version, DateTime now)
    {
        EnsureMutable(version, now);
        // Tombstone فقط شناسه و نسخه را نگه می‌دارد؛ محتوای مخاطب حذف‌شده دوباره قابل نمایش نیست.
        ProtectedContent = ""; LinkedAccountUserId = null; HasManualName = false;
        DeletedAtUtc = now; UpdatedAtUtc = now; ServerVersion = version;
    }
    private void EnsureMutable(long version, DateTime now)
    {
        if (DeletedAtUtc.HasValue || version <= ServerVersion || now.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("نسخهٔ مخاطب قابل تغییر نیست؛ مخاطب حذف‌شده با همان شناسه احیا نمی‌شود.");
    }
}

public sealed class ContactPhone
{
    private ContactPhone() { }
    public Guid OwnerUserId { get; private set; }
    public Guid EntryId { get; private set; }
    public string PhoneIndex { get; private set; } = "";
    public static ContactPhone Create(Guid owner, Guid entry, string index) => new() { OwnerUserId = owner, EntryId = entry, PhoneIndex = index };
}

public sealed class ContactSyncDevice
{
    private ContactSyncDevice() { }
    public Guid OwnerUserId { get; private set; }
    public Guid DeviceId { get; private set; }
    public string ConsentVersion { get; private set; } = "";
    public string Scope { get; private set; } = "";
    public Guid ConsentId { get; private set; }
    public DateTime GrantedAtUtc { get; private set; }
    public bool SyncEnabled { get; private set; }
    public long LastSourceRevision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static ContactSyncDevice Create(Guid owner, Guid device) => new() { OwnerUserId = owner, DeviceId = device };
    public void Consent(string version, string scope, bool enabled, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length > 64 || string.IsNullOrWhiteSpace(scope) || scope.Length > 64 || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("رضایت همگام‌سازی معتبر نیست.");
        // تغییر رضایت cursor قبلی را بی‌اعتبار می‌کند، ولی دفترچه را پاک نمی‌کند.
        ConsentVersion = version; Scope = scope; SyncEnabled = enabled; ConsentId = Guid.NewGuid(); GrantedAtUtc = now;
    }
    public void Advance(long revision)
    {
        if (revision <= LastSourceRevision) throw new InvalidOperationException("نسخهٔ دستگاه قدیمی است.");
        LastSourceRevision = revision;
    }
}

public sealed class ContactDeviceBinding
{
    private ContactDeviceBinding() { }
    public Guid OwnerUserId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid EntryId { get; private set; }
    public long SourceRevision { get; private set; }
    public long ServerVersion { get; private set; }
    public static ContactDeviceBinding Create(Guid owner, Guid device, Guid entry) => new() { OwnerUserId = owner, DeviceId = device, EntryId = entry };
    public void Advance(long source, long server) { SourceRevision = source; ServerVersion = server; }
}

public sealed class ContactSyncState
{
    private ContactSyncState() { }
    public Guid OwnerUserId { get; private set; }
    public long ServerVersion { get; private set; }
    public static ContactSyncState Create(Guid owner) => new() { OwnerUserId = owner };
    public long NextVersion() => ServerVersion = checked(ServerVersion + 1);
}

public sealed class ContactSyncOperation
{
    private ContactSyncOperation() { }
    public Guid OwnerUserId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public string RequestFingerprint { get; private set; } = "";
    public string Receipt { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public static ContactSyncOperation Create(Guid owner, Guid device, Guid operation, string hash, string receipt, DateTime now) =>
        new() { OwnerUserId = owner, DeviceId = device, ClientOperationId = operation, RequestFingerprint = hash, Receipt = receipt, CreatedAtUtc = now };
}
