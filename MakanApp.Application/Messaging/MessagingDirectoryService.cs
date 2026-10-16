using MakanApp.Application.Identity;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed class MessagingDirectoryService(
    IMessagingDirectoryStore store,
    MessagingOptions options,
    TimeProvider timeProvider) : IMessagingDirectoryService
{
    public async Task<UserContactPageResult> GetContactsAsync(
        Guid userId,
        string? query,
        bool favoritesOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (skip, take) = ValidatePage(page, pageSize);
        var normalizedQuery = NormalizeOptionalQuery(query);
        var result = await store.GetContactsAsync(
            userId,
            normalizedQuery,
            favoritesOnly,
            skip,
            take,
            cancellationToken);
        return new UserContactPageResult(
            result.Items.Select(item => MapContact(item)).ToArray(),
            page,
            pageSize,
            result.HasMore);
    }

    public async Task<UserContactResult> SaveContactAsync(
        Guid userId,
        SaveUserContactCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ContactUserId == Guid.Empty)
        {
            throw Error(MessagingErrorCodes.ContactTargetNotAvailable, "مخاطب قابل ذخیره یافت نشد.");
        }

        if (command.ContactUserId == userId)
        {
            throw Error(MessagingErrorCodes.ContactSelfNotAllowed, "ذخیره حساب خود به‌عنوان مخاطب مجاز نیست.");
        }

        var savedName = NormalizeSavedName(command.SavedName);
        var result = await store.SaveContactAsync(
            userId,
            command.ContactUserId,
            savedName,
            UtcNow(),
            cancellationToken);
        return MapContact(
            new UserContactStoreRecord(result.Contact, result.Profile),
            result.AlreadyExisted);
    }

    public async Task<UserContactResult> UpdateContactAsync(
        Guid userId,
        Guid contactUserId,
        UpdateUserContactCommand command,
        CancellationToken cancellationToken)
    {
        if (contactUserId == Guid.Empty || contactUserId == userId)
        {
            throw Error(MessagingErrorCodes.ContactNotFound, "مخاطب ذخیره‌شده یافت نشد.");
        }

        var record = await store.UpdateContactAsync(
            userId,
            contactUserId,
            NormalizeSavedName(command.SavedName),
            command.IsFavorite,
            DecodeVersion(command.ExpectedVersion, MessagingErrorCodes.ContactConflict),
            UtcNow(),
            cancellationToken);
        return MapContact(record);
    }

    public Task DeleteContactAsync(
        Guid userId,
        Guid contactUserId,
        CancellationToken cancellationToken)
    {
        if (contactUserId == Guid.Empty || contactUserId == userId)
        {
            return Task.CompletedTask;
        }

        return store.DeleteContactAsync(userId, contactUserId, cancellationToken);
    }

    public async Task<RecentContactPageResult> GetRecentContactsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (skip, take) = ValidatePage(page, pageSize);
        var result = await store.GetRecentContactsAsync(
            userId,
            skip,
            take,
            cancellationToken);
        return new RecentContactPageResult(
            result.Items
                .Select(item => new RecentContactResult(
                    MapProfile(userId, item.Profile),
                    item.LastActivityAtUtc))
                .ToArray(),
            page,
            pageSize,
            result.HasMore);
    }

    public async Task<PeopleSearchPageResult> SearchPeopleAsync(
        Guid userId,
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery) ||
            normalizedQuery.Length < options.MinimumPeopleSearchLength ||
            normalizedQuery.Length > options.MaximumPeopleSearchLength)
        {
            throw Error(
                MessagingErrorCodes.PeopleSearchInvalid,
                $"عبارت جست‌وجو باید بین {options.MinimumPeopleSearchLength} و {options.MaximumPeopleSearchLength} نویسه باشد.");
        }

        var (skip, take) = ValidatePage(page, pageSize);
        var result = await store.SearchPeopleAsync(
            userId,
            normalizedQuery,
            skip,
            take,
            cancellationToken);
        return new PeopleSearchPageResult(
            result.Items.Select(item => MapProfile(userId, item, includePhoneNumber: false)).ToArray(),
            page,
            pageSize,
            result.HasMore);
    }

    public async Task<PeopleResolveResult> ResolvePersonAsync(
        Guid userId,
        ResolvePersonCommand command,
        CancellationToken cancellationToken)
    {
        var hasUsername = !string.IsNullOrWhiteSpace(command.Username);
        var hasPhone = !string.IsNullOrWhiteSpace(command.PhoneNumber);
        if (hasUsername == hasPhone)
        {
            throw Error(
                MessagingErrorCodes.PeopleResolveInvalid,
                "دقیقاً یکی از نام کاربری یا شماره تلفن باید ارسال شود.");
        }

        MessagingProfileStoreRecord? record;
        if (hasPhone)
        {
            if (!PhoneNumberNormalizer.TryNormalize(command.PhoneNumber, out var normalizedPhone))
            {
                throw Error(
                    MessagingErrorCodes.PeopleResolveInvalid,
                    "شماره تلفن برای جست‌وجوی دقیق معتبر نیست.");
            }

            record = await store.FindByPhoneAsync(userId, normalizedPhone, cancellationToken);
            if (record is null || !MessagingPrivacyPolicy.Allows(
                    record.PhoneNumberDiscoveryAudience,
                    record.TargetHasSavedRequester,
                    record.UserId == userId))
            {
                return new PeopleResolveResult(false, null);
            }
        }
        else
        {
            var normalizedUsername = NormalizeUsername(command.Username!);
            record = await store.FindByUsernameAsync(
                userId,
                normalizedUsername,
                cancellationToken);
            if (record is null)
            {
                return new PeopleResolveResult(false, null);
            }
        }

        return new PeopleResolveResult(true, MapProfile(userId, record, includePhoneNumber: hasPhone));
    }

    public async Task<MessagingPublicProfileResult> GetPublicProfileAsync(
        Guid userId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        if (targetUserId == Guid.Empty)
        {
            throw PersonNotDiscoverable();
        }

        var record = await store.FindByUserIdAsync(userId, targetUserId, cancellationToken)
            ?? throw PersonNotDiscoverable();
        return MapProfile(userId, record);
    }

    public async Task<MessagingPrivacySettingsResult> GetPrivacySettingsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var settings = await store.GetPrivacySettingsAsync(userId, cancellationToken);
        return settings is null
            ? MapPrivacy(MessagingPrivacyPolicy.Defaults())
            : MapPrivacy(new EffectiveMessagingPrivacySettings(
                settings.DirectMessagesAudience,
                settings.PhoneNumberVisibilityAudience,
                settings.PhoneNumberDiscoveryAudience,
                settings.GroupInvitesAudience,
                true,
                settings.RowVersion));
    }

    public async Task<MessagingPrivacySettingsResult> UpdatePrivacySettingsAsync(
        Guid userId,
        UpdateMessagingPrivacyCommand command,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(command.DirectMessagesAudience) ||
            !Enum.IsDefined(command.PhoneNumberVisibilityAudience) ||
            !Enum.IsDefined(command.PhoneNumberDiscoveryAudience) ||
            !Enum.IsDefined(command.GroupInvitesAudience))
        {
            throw Error(MessagingErrorCodes.PrivacyInvalid, "تنظیم حریم خصوصی معتبر نیست.");
        }

        var expectedVersion = string.IsNullOrWhiteSpace(command.ExpectedVersion)
            ? null
            : DecodeVersion(command.ExpectedVersion, MessagingErrorCodes.PrivacyConflict);
        var settings = await store.SavePrivacySettingsAsync(
            userId,
            command.DirectMessagesAudience,
            command.PhoneNumberVisibilityAudience,
            command.PhoneNumberDiscoveryAudience,
            command.GroupInvitesAudience,
            expectedVersion,
            UtcNow(),
            cancellationToken);
        return MapPrivacy(new EffectiveMessagingPrivacySettings(
            settings.DirectMessagesAudience,
            settings.PhoneNumberVisibilityAudience,
            settings.PhoneNumberDiscoveryAudience,
            settings.GroupInvitesAudience,
            true,
            settings.RowVersion));
    }

    private UserContactResult MapContact(UserContactStoreRecord record, bool alreadyExisted = false) => new(
        record.Contact.ContactUserId,
        record.Contact.SavedName,
        record.Contact.IsFavorite,
        MapProfile(record.Contact.OwnerUserId, record.Profile),
        record.Contact.CreatedAtUtc,
        record.Contact.UpdatedAtUtc,
        Convert.ToBase64String(record.Contact.RowVersion),
        alreadyExisted);

    private static MessagingPublicProfileResult MapProfile(
        Guid requesterUserId,
        MessagingProfileStoreRecord record,
        bool includePhoneNumber = true)
    {
        var requesterIsTarget = requesterUserId == record.UserId;
        var phoneVisible = includePhoneNumber && (!record.IsProtectedAccount || requesterIsTarget) && MessagingPrivacyPolicy.Allows(
            record.PhoneNumberVisibilityAudience,
            record.TargetHasSavedRequester,
            requesterIsTarget);
        var canStartDirect = !requesterIsTarget &&
            record.SafetyAllowsDirect &&
            !record.HasEffectiveDirectBlock &&
            MessagingPrivacyPolicy.Allows(
                record.DirectMessagesAudience,
                record.TargetHasSavedRequester);
        return new MessagingPublicProfileResult(
            record.UserId,
            record.Username,
            record.DisplayName,
            phoneVisible ? record.NormalizedPhoneNumber : null,
            record.IsSavedContact,
            record.IsFavorite,
            canStartDirect);
    }

    private static MessagingPrivacySettingsResult MapPrivacy(
        EffectiveMessagingPrivacySettings settings) => new(
        settings.DirectMessagesAudience,
        settings.PhoneNumberVisibilityAudience,
        settings.PhoneNumberDiscoveryAudience,
        settings.GroupInvitesAudience,
        settings.IsExplicit,
        settings.Version is null ? null : Convert.ToBase64String(settings.Version));

    private (int Skip, int Take) ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > options.MaximumDirectoryPageSize ||
            (long)(page - 1) * pageSize > int.MaxValue)
        {
            throw Error(
                MessagingErrorCodes.DirectoryPageInvalid,
                $"شماره صفحه باید مثبت و اندازه صفحه بین ۱ تا {options.MaximumDirectoryPageSize} باشد.");
        }

        return ((page - 1) * pageSize, pageSize);
    }

    private string? NormalizeOptionalQuery(string? query)
    {
        var normalized = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        if (normalized?.Length > options.MaximumPeopleSearchLength)
        {
            throw Error(MessagingErrorCodes.PeopleSearchInvalid, "عبارت جست‌وجو بیش از حد مجاز است.");
        }

        return normalized;
    }

    private static string? NormalizeSavedName(string? value)
    {
        try
        {
            return UserContact.NormalizeSavedName(value);
        }
        catch (ArgumentException exception)
        {
            throw Error(MessagingErrorCodes.ContactInvalid, exception.Message);
        }
    }

    private static string NormalizeUsername(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 3 or > 32 ||
            normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_'))
        {
            throw Error(MessagingErrorCodes.PeopleResolveInvalid, "نام کاربری برای جست‌وجوی دقیق معتبر نیست.");
        }

        return normalized.ToUpperInvariant();
    }

    private static byte[] DecodeVersion(string? value, string errorCode)
    {
        try
        {
            var decoded = Convert.FromBase64String(value ?? string.Empty);
            if (decoded.Length != 8)
            {
                throw new FormatException();
            }

            return decoded;
        }
        catch (FormatException)
        {
            throw Error(errorCode, "نسخه مورد انتظار معتبر نیست.");
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static MessagingException PersonNotDiscoverable() => Error(
        MessagingErrorCodes.PersonNotDiscoverable,
        "پروفایل قابل مشاهده یافت نشد.");

    private static MessagingException Error(string code, string message) => new(code, message);
}
