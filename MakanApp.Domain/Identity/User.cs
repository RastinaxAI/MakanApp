namespace MakanApp.Domain.Identity;

public sealed class User
{
    public const int MaximumBioLength = 500;
    public const string DeletedUsernamePrefix = "$del_";
    private User()
    {
    }

    private User(Guid id, DateTime createdAtUtc)
    {
        Id = id;
        AccountStatus = UserAccountStatus.Active;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? PersonId { get; private set; }
    public Guid? ProfilePhotoFileAssetId { get; private set; }
    public string? Username { get; private set; }
    public string? NormalizedUsername { get; private set; }
    public string? Bio { get; private set; }
    public CommunicationAgeCategory CommunicationAgeCategory { get; private set; }
    public CommunicationAgeVerificationStatus CommunicationAgeVerificationStatus { get; private set; }
    public DateTime? MessagingRestrictedUntilUtc { get; private set; }
    public UserAccountStatus AccountStatus { get; private set; }
    public DateTime? SuspendedAtUtc { get; private set; }
    public DateTime? DeletionStartedAtUtc { get; private set; }
    public DateTime? AnonymizedAtUtc { get; private set; }
    public DateTime? ProfileCompletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsProfileComplete =>
        !AnonymizedAtUtc.HasValue && PersonId.HasValue && !string.IsNullOrWhiteSpace(Username);

    public static User Create(DateTime createdAtUtc) =>
        new(Guid.NewGuid(), createdAtUtc);

    public void SetBio(string? value, DateTime nowUtc)
    {
        var bio = value?.Trim();
        if (bio?.Length > MaximumBioLength || bio?.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t') == true)
            throw new ArgumentException("متن معرفی معتبر نیست.", nameof(value));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("زمان تغییر باید UTC باشد.", nameof(nowUtc));
        Bio = string.IsNullOrEmpty(bio) ? null : bio;
        UpdatedAtUtc = nowUtc;
    }

    public void SetProfilePhoto(Guid? fileAssetId, DateTime updatedAtUtc)
    {
        if (fileAssetId == Guid.Empty)
            throw new ArgumentException("شناسهٔ عکس پروفایل معتبر نیست.", nameof(fileAssetId));
        if (updatedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("زمان تغییر عکس پروفایل باید UTC باشد.", nameof(updatedAtUtc));
        if (ProfilePhotoFileAssetId == fileAssetId) return;
        ProfilePhotoFileAssetId = fileAssetId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Suspend(DateTime suspendedAtUtc)
    {
        if (suspendedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان تعلیق حساب باید UTC باشد.", nameof(suspendedAtUtc));
        }

        if (AccountStatus == UserAccountStatus.Suspended)
        {
            return;
        }

        AccountStatus = UserAccountStatus.Suspended;
        SuspendedAtUtc = suspendedAtUtc;
        UpdatedAtUtc = suspendedAtUtc;
    }

    public void Reactivate(DateTime reactivatedAtUtc)
    {
        if (DeletionStartedAtUtc.HasValue)
            throw new InvalidOperationException("حساب واردشده به اجرای حذف قابل فعال‌سازی مجدد نیست.");
        if (reactivatedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان فعال‌سازی حساب باید UTC باشد.", nameof(reactivatedAtUtc));
        }

        if (AccountStatus == UserAccountStatus.Active)
        {
            return;
        }

        AccountStatus = UserAccountStatus.Active;
        SuspendedAtUtc = null;
        UpdatedAtUtc = reactivatedAtUtc;
    }

    public void SetCommunicationAgeCategory(
        CommunicationAgeCategory communicationAgeCategory,
        DateTime updatedAtUtc)
    {
        if (updatedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("زمان تأیید سن باید UTC باشد.", nameof(updatedAtUtc));
        if (!Enum.IsDefined(communicationAgeCategory))
        {
            throw new ArgumentOutOfRangeException(nameof(communicationAgeCategory));
        }

        CommunicationAgeCategory = communicationAgeCategory;
        CommunicationAgeVerificationStatus = communicationAgeCategory == CommunicationAgeCategory.Unknown
            ? CommunicationAgeVerificationStatus.Unverified : CommunicationAgeVerificationStatus.Verified;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void MarkCommunicationAgeConflict(DateTime nowUtc)
    {
        SetCommunicationAgeCategory(CommunicationAgeCategory.Unknown, nowUtc);
        CommunicationAgeVerificationStatus = CommunicationAgeVerificationStatus.Conflict;
    }

    public void RestrictMessaging(DateTime untilUtc, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc || untilUtc.Kind != DateTimeKind.Utc || untilUtc <= nowUtc)
            throw new ArgumentException("زمان پایان محدودیت باید UTC و در آینده باشد.");
        MessagingRestrictedUntilUtc = MessagingRestrictedUntilUtc > untilUtc ? MessagingRestrictedUntilUtc : untilUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void SetProfile(
        Guid personId,
        string username,
        string normalizedUsername,
        DateTime updatedAtUtc)
    {
        if (DeletionStartedAtUtc.HasValue) throw new InvalidOperationException("پروفایل حساب در حال حذف قابل تغییر نیست.");
        PersonId = personId;
        Username = username;
        NormalizedUsername = normalizedUsername;
        UpdatedAtUtc = updatedAtUtc;
        ProfileCompletedAtUtc ??= updatedAtUtc;
    }

    public void BeginAccountDeletion(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc) throw new ArgumentException("زمان باید UTC باشد.");
        Suspend(now); DeletionStartedAtUtc ??= now;
    }

    public void Anonymize(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc || !DeletionStartedAtUtc.HasValue || now < DeletionStartedAtUtc || AccountStatus != UserAccountStatus.Suspended)
            throw new InvalidOperationException("حساب ابتدا باید برای حذف مسدود شده باشد.");
        if (AnonymizedAtUtc.HasValue) return;
        Username = CreateDeletedUsername(Id); NormalizedUsername = Username.ToUpperInvariant();
        Bio = null; ProfilePhotoFileAssetId = null;
        CommunicationAgeCategory = CommunicationAgeCategory.Unknown;
        CommunicationAgeVerificationStatus = CommunicationAgeVerificationStatus.Unverified;
        AnonymizedAtUtc = now; UpdatedAtUtc = now;
    }

    public static string CreateDeletedUsername(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("شناسهٔ حساب لازم است.", nameof(userId));
        // این پیشوند در ورودی عمومی username مجاز نیست؛ Base32 تمام ۱۲۸ بیت شناسه را بدون برخورد نگه می‌دارد.
        const string alphabet = "0123456789abcdefghjkmnpqrstvwxyz";
        var suffix = new System.Text.StringBuilder(26); var buffer = 0; var bits = 0;
        foreach (var value in userId.ToByteArray())
        {
            buffer = (buffer << 8) | value; bits += 8;
            while (bits >= 5) { bits -= 5; suffix.Append(alphabet[(buffer >> bits) & 31]); }
            buffer &= (1 << bits) - 1;
        }
        if (bits > 0) suffix.Append(alphabet[(buffer << (5 - bits)) & 31]);
        return DeletedUsernamePrefix + suffix;
    }
}
