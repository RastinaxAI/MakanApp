using System.Data;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed class EfMessagingDirectoryStore(MakanDbContext dbContext, TimeProvider clock,
    MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime)
    : IMessagingDirectoryStore
{
    public async Task<UserContactStorePage> GetContactsAsync(
        Guid ownerUserId,
        string? query,
        bool favoritesOnly,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var contactsQuery =
            from contact in dbContext.UserContacts.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on contact.ContactUserId equals user.Id
            join person in dbContext.Persons.AsNoTracking() on user.PersonId equals person.Id
            where contact.OwnerUserId == ownerUserId &&
                  user.PersonId != null &&
                  user.Username != null &&
                  user.ProfileCompletedAtUtc != null &&
                  (!favoritesOnly || contact.IsFavorite)
            let displayName = person.DisplayName ?? person.FirstName + " " + person.LastName
            where query == null ||
                  contact.SavedName != null && contact.SavedName.Contains(query) ||
                  user.Username!.Contains(query) ||
                  displayName.Contains(query)
            orderby contact.SavedName ?? displayName, user.NormalizedUsername, contact.ContactUserId
            select contact;

        var rows = await contactsQuery
            .Skip(skip)
            .Take(take + 1)
            .ToArrayAsync(cancellationToken);
        var hasMore = rows.Length > take;
        var pageRows = rows.Take(take).ToArray();
        var profiles = await LoadProfilesAsync(
            ownerUserId,
            pageRows.Select(contact => contact.ContactUserId),
            cancellationToken);
        var result = pageRows
            .Where(contact => profiles.ContainsKey(contact.ContactUserId))
            .Select(contact => new UserContactStoreRecord(
                contact,
                profiles[contact.ContactUserId]))
            .ToArray();
        return new UserContactStorePage(result, hasMore);
    }

    public async Task<SaveUserContactStoreResult> SaveContactAsync(
        Guid ownerUserId,
        Guid contactUserId,
        string? savedName,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            if (!await IsDiscoverableAccountAsync(contactUserId, cancellationToken))
            {
                throw ContactTargetNotAvailable();
            }

            var existing = await dbContext.UserContacts
                .FromSqlInterpolated($"SELECT * FROM [messaging].[UserContacts] WITH (UPDLOCK, HOLDLOCK) WHERE [OwnerUserId] = {ownerUserId} AND [ContactUserId] = {contactUserId}")
                .SingleOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                var existingProfile = await LoadProfileAsync(
                    ownerUserId,
                    contactUserId,
                    cancellationToken) ?? throw ContactTargetNotAvailable();
                await transaction.CommitAsync(cancellationToken);
                return new SaveUserContactStoreResult(existing, existingProfile, true);
            }

            var contact = UserContact.Create(ownerUserId, contactUserId, savedName, nowUtc);
            dbContext.UserContacts.Add(contact);
            realtime.CaptureMessaging();
            await dbContext.SaveChangesAsync(cancellationToken);
            var profile = await LoadProfileAsync(ownerUserId, contactUserId, cancellationToken)
                ?? throw ContactTargetNotAvailable();
            await transaction.CommitAsync(cancellationToken);
            return new SaveUserContactStoreResult(contact, profile, false);
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            var existing = await dbContext.UserContacts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    contact => contact.OwnerUserId == ownerUserId &&
                               contact.ContactUserId == contactUserId,
                    cancellationToken);
            var profile = await LoadProfileAsync(ownerUserId, contactUserId, cancellationToken);
            if (existing is null || profile is null)
            {
                throw ContactConflict(exception);
            }

            return new SaveUserContactStoreResult(existing, profile, true);
        }
    }

    public async Task<UserContactStoreRecord> UpdateContactAsync(
        Guid ownerUserId,
        Guid contactUserId,
        string? savedName,
        bool isFavorite,
        byte[] expectedVersion,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var contact = await dbContext.UserContacts.SingleOrDefaultAsync(
            item => item.OwnerUserId == ownerUserId && item.ContactUserId == contactUserId,
            cancellationToken) ?? throw ContactNotFound();
        dbContext.Entry(contact).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        contact.Update(savedName, isFavorite, nowUtc);
        try
        {
            realtime.CaptureMessaging();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw ContactConflict(exception);
        }

        var profile = await LoadProfileAsync(ownerUserId, contactUserId, cancellationToken)
            ?? throw ContactNotFound();
        return new UserContactStoreRecord(contact, profile);
    }

    public async Task DeleteContactAsync(
        Guid ownerUserId,
        Guid contactUserId,
        CancellationToken cancellationToken)
    {
        var contact = await dbContext.UserContacts.SingleOrDefaultAsync(
            item => item.OwnerUserId == ownerUserId && item.ContactUserId == contactUserId,
            cancellationToken);
        if (contact is null)
        {
            return;
        }

        dbContext.UserContacts.Remove(contact);
        realtime.CaptureMessaging();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RecentContactStorePage> GetRecentContactsAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from conversation in dbContext.Conversations.AsNoTracking()
            join participant in dbContext.ConversationParticipants.AsNoTracking()
                on conversation.Id equals participant.ConversationId
            where conversation.Type == ConversationType.Direct &&
                  conversation.Scope == ConversationScope.Personal &&
                  conversation.OrganizationId == null &&
                  participant.UserId == userId &&
                  participant.Status == ConversationParticipantStatus.Active &&
                  participant.EndedAtUtc == null
            let otherUserId = conversation.DirectUserLowId == userId
                ? conversation.DirectUserHighId!.Value
                : conversation.DirectUserLowId!.Value
            let lastMessageAtUtc = dbContext.Messages
                .Where(message => message.ConversationId == conversation.Id)
                .Max(message => (DateTime?)message.SentAtUtc)
            orderby lastMessageAtUtc ?? conversation.CreatedAtUtc descending, conversation.Id
            select new RecentRow(
                otherUserId,
                lastMessageAtUtc ?? conversation.CreatedAtUtc))
            .Skip(skip)
            .Take(take + 1)
            .ToArrayAsync(cancellationToken);
        var hasMore = rows.Length > take;
        var pageRows = rows.Take(take).ToArray();
        var profiles = await LoadProfilesAsync(
            userId,
            pageRows.Select(row => row.UserId),
            cancellationToken);
        return new RecentContactStorePage(
            pageRows
                .Where(row => profiles.ContainsKey(row.UserId))
                .Select(row => new RecentContactStoreRecord(
                    profiles[row.UserId],
                    row.LastActivityAtUtc))
                .ToArray(),
            hasMore);
    }

    public async Task<MessagingProfileStorePage> SearchPeopleAsync(
        Guid requesterUserId,
        string query,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedUsernameQuery = query.ToUpperInvariant();
        var userIds = await (
            from user in DiscoverableAccounts(requesterUserId)
            join person in dbContext.Persons.AsNoTracking() on user.PersonId equals person.Id
            where user.Id != requesterUserId &&
                  user.PersonId != null &&
                  user.Username != null &&
                  user.NormalizedUsername != null &&
                  user.ProfileCompletedAtUtc != null
            let displayName = person.DisplayName ?? person.FirstName + " " + person.LastName
            where user.NormalizedUsername!.Contains(normalizedUsernameQuery) ||
                  displayName.Contains(query)
            orderby user.NormalizedUsername == normalizedUsernameQuery ? 0 : 1,
                user.NormalizedUsername,
                user.Id
            select user.Id)
            .Skip(skip)
            .Take(take + 1)
            .ToArrayAsync(cancellationToken);
        var hasMore = userIds.Length > take;
        var pageIds = userIds.Take(take).ToArray();
        var profiles = await LoadProfilesAsync(requesterUserId, pageIds, cancellationToken);
        return new MessagingProfileStorePage(
            pageIds.Where(profiles.ContainsKey).Select(id => profiles[id]).ToArray(),
            hasMore);
    }

    public async Task<MessagingProfileStoreRecord?> FindByUsernameAsync(
        Guid requesterUserId,
        string normalizedUsername,
        CancellationToken cancellationToken)
    {
        var userId = await DiscoverableAccounts(requesterUserId)
            .Where(user => user.NormalizedUsername == normalizedUsername &&
                           user.ProfileCompletedAtUtc != null &&
                           user.PersonId != null)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return userId.HasValue
            ? await LoadProfileAsync(requesterUserId, userId.Value, cancellationToken)
            : null;
    }

    public async Task<MessagingProfileStoreRecord?> FindByPhoneAsync(
        Guid requesterUserId,
        string normalizedPhoneNumber,
        CancellationToken cancellationToken)
    {
        var userId = await (
            from credential in dbContext.UserCredentials.AsNoTracking()
            join user in DiscoverableAccounts(requesterUserId) on credential.UserId equals user.Id
            where credential.Kind == CredentialKind.MobilePhone &&
                  credential.NormalizedIdentifier == normalizedPhoneNumber &&
                  user.ProfileCompletedAtUtc != null &&
                  user.PersonId != null
            select (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return userId.HasValue
            ? await LoadProfileAsync(requesterUserId, userId.Value, cancellationToken)
            : null;
    }

    public Task<MessagingProfileStoreRecord?> FindByUserIdAsync(
        Guid requesterUserId,
        Guid targetUserId,
        CancellationToken cancellationToken) =>
        LoadProfileAsync(requesterUserId, targetUserId, cancellationToken);

    public Task<MessagingPrivacySettings?> GetPrivacySettingsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.MessagingPrivacySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(settings => settings.UserId == userId, cancellationToken);

    public async Task<MessagingPrivacySettings> SavePrivacySettingsAsync(
        Guid userId,
        PrivacyAudience directMessagesAudience,
        PrivacyAudience phoneNumberVisibilityAudience,
        PrivacyAudience phoneNumberDiscoveryAudience,
        PrivacyAudience groupInvitesAudience,
        byte[]? expectedVersion,
        DateTime nowUtc,
        CancellationToken cancellationToken,
        PrivacyAudience? onlineAudience = null,
        PrivacyAudience? lastSeenAudience = null)
    {
        var settings = await dbContext.MessagingPrivacySettings
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (settings is null)
        {
            if (expectedVersion is not null)
            {
                throw PrivacyConflict();
            }

            settings = MessagingPrivacySettings.Create(
                userId,
                directMessagesAudience,
                phoneNumberVisibilityAudience,
                phoneNumberDiscoveryAudience,
                groupInvitesAudience,
                nowUtc);
            dbContext.MessagingPrivacySettings.Add(settings);
        }
        else
        {
            if (expectedVersion is null)
            {
                throw PrivacyConflict();
            }

            dbContext.Entry(settings).Property(item => item.RowVersion).OriginalValue = expectedVersion;
            settings.Update(
                directMessagesAudience,
                phoneNumberVisibilityAudience,
                phoneNumberDiscoveryAudience,
                groupInvitesAudience,
                nowUtc);
        }

        settings.UpdatePresenceAudiences(onlineAudience, lastSeenAudience);
        try
        {
            realtime.CaptureMessaging();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw PrivacyConflict(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            throw PrivacyConflict(exception);
        }

        return settings;
    }

    private async Task<MessagingProfileStoreRecord?> LoadProfileAsync(
        Guid requesterUserId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var profiles = await LoadProfilesAsync(
            requesterUserId,
            [targetUserId],
            cancellationToken);
        return profiles.GetValueOrDefault(targetUserId);
    }

    private async Task<Dictionary<Guid, MessagingProfileStoreRecord>> LoadProfilesAsync(
        Guid requesterUserId,
        IEnumerable<Guid> requestedUserIds,
        CancellationToken cancellationToken)
    {
        var userIds = requestedUserIds.Distinct().ToArray();
        if (userIds.Length == 0)
        {
            return [];
        }

        var identities = await (
            from user in DiscoverableAccounts(requesterUserId)
            join person in dbContext.Persons.AsNoTracking() on user.PersonId equals person.Id
            where userIds.Contains(user.Id) &&
                  user.PersonId != null &&
                  user.Username != null &&
                  user.ProfileCompletedAtUtc != null
            join file in MakanApp.Infrastructure.Identity.ProfilePhotoQuery.ReadyImages(dbContext)
                on user.ProfilePhotoFileAssetId equals file.Id into photos
            from file in photos.DefaultIfEmpty()
            select new IdentityRow(
                user.Id,
                user.Username!,
                person.DisplayName ?? person.FirstName + " " + person.LastName,
                dbContext.UserCredentials
                    .Where(credential => credential.UserId == user.Id &&
                                         credential.Kind == CredentialKind.MobilePhone)
                    .Select(credential => credential.NormalizedIdentifier)
                    .FirstOrDefault(),
                user.CommunicationAgeCategory != CommunicationAgeCategory.Adult ||
                user.CommunicationAgeVerificationStatus != CommunicationAgeVerificationStatus.Verified,
                file != null && file.UploadedByUserId == user.Id
                    ? new MakanApp.Application.Identity.ProfilePhotoResult(file.Id, file.ContentType) : null))
            .ToArrayAsync(cancellationToken);
        var validUserIds = identities.Select(identity => identity.UserId).ToArray();
        if (validUserIds.Length == 0)
        {
            return [];
        }

        var settings = await dbContext.MessagingPrivacySettings.AsNoTracking()
            .Where(item => validUserIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, cancellationToken);
        var requesterContacts = await dbContext.UserContacts.AsNoTracking()
            .Where(contact => contact.OwnerUserId == requesterUserId &&
                              validUserIds.Contains(contact.ContactUserId))
            .ToDictionaryAsync(contact => contact.ContactUserId, cancellationToken);
        var savedRequesterUserIds = await dbContext.UserContacts.AsNoTracking()
            .Where(contact => validUserIds.Contains(contact.OwnerUserId) &&
                              contact.ContactUserId == requesterUserId)
            .Select(contact => contact.OwnerUserId)
            .ToHashSetAsync(cancellationToken);
        var blockedUserIds = await dbContext.UserBlocks.AsNoTracking()
            .Where(block => block.Status == UserBlockStatus.Active &&
                            block.EndedAtUtc == null &&
                            (block.BlockerUserId == requesterUserId &&
                             validUserIds.Contains(block.BlockedUserId) ||
                             block.BlockedUserId == requesterUserId &&
                             validUserIds.Contains(block.BlockerUserId)))
            .Select(block => block.BlockerUserId == requesterUserId
                ? block.BlockedUserId
                : block.BlockerUserId)
            .ToHashSetAsync(cancellationToken);

        var requesterIsAdult = await dbContext.Users.AnyAsync(u => u.Id == requesterUserId &&
            u.CommunicationAgeCategory == CommunicationAgeCategory.Adult &&
            u.CommunicationAgeVerificationStatus == CommunicationAgeVerificationStatus.Verified, cancellationToken);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var requesterCanMessage = await dbContext.Users.AnyAsync(u => u.Id == requesterUserId &&
            u.AccountStatus == UserAccountStatus.Active &&
            (u.MessagingRestrictedUntilUtc == null || u.MessagingRestrictedUntilUtc <= nowUtc), cancellationToken);
        var relatedIds = requesterIsAdult && identities.All(i => !i.IsProtected) ? [] :
            await MessengerRelationshipQuery.RelatedUsers(dbContext, requesterUserId, clock.GetUtcNow().UtcDateTime)
                .Where(id => validUserIds.Contains(id)).ToHashSetAsync(cancellationToken);
        var result = new Dictionary<Guid, MessagingProfileStoreRecord>(identities.Length);
        foreach (var identity in identities)
        {
            var privacy = settings.GetValueOrDefault(identity.UserId);
            var contact = requesterContacts.GetValueOrDefault(identity.UserId);
            result[identity.UserId] = new MessagingProfileStoreRecord(
                identity.UserId,
                identity.Username,
                identity.DisplayName,
                identity.NormalizedPhoneNumber,
                privacy?.DirectMessagesAudience ?? MessagingPrivacySettings.DefaultDirectMessagesAudience,
                privacy?.PhoneNumberVisibilityAudience ?? MessagingPrivacySettings.DefaultPhoneNumberVisibilityAudience,
                privacy?.PhoneNumberDiscoveryAudience ?? MessagingPrivacySettings.DefaultPhoneNumberDiscoveryAudience,
                savedRequesterUserIds.Contains(identity.UserId),
                contact is not null,
                contact?.IsFavorite ?? false,
                blockedUserIds.Contains(identity.UserId),
                identity.IsProtected,
                requesterCanMessage && (requesterIsAdult && !identity.IsProtected || relatedIds.Contains(identity.UserId)),
                identity.ProfilePhoto);
        }

        return result;
    }

    private Task<bool> IsDiscoverableAccountAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().AnyAsync(
            user => user.Id == userId &&
                    user.PersonId != null &&
                    user.Username != null &&
                    user.ProfileCompletedAtUtc != null,
            cancellationToken);

    private IQueryable<User> DiscoverableAccounts(Guid requesterId)
        => MessengerRelationshipQuery.DiscoverableAccounts(dbContext, requesterId, clock.GetUtcNow().UtcDateTime);

    private static bool IsUniqueConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static MessagingException ContactTargetNotAvailable() => new(
        MessagingErrorCodes.ContactTargetNotAvailable,
        "مخاطب قابل ذخیره یافت نشد.");

    private static MessagingException ContactNotFound() => new(
        MessagingErrorCodes.ContactNotFound,
        "مخاطب ذخیره‌شده یافت نشد.");

    private static MessagingException ContactConflict(Exception? exception = null) => new(
        MessagingErrorCodes.ContactConflict,
        "مخاطب هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.",
        exception);

    private static MessagingException PrivacyConflict(Exception? exception = null) => new(
        MessagingErrorCodes.PrivacyConflict,
        "تنظیمات حریم خصوصی هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.",
        exception);

    private sealed record IdentityRow(
        Guid UserId,
        string Username,
        string DisplayName,
        string? NormalizedPhoneNumber,
        bool IsProtected,
        MakanApp.Application.Identity.ProfilePhotoResult? ProfilePhoto);

    private sealed record RecentRow(Guid UserId, DateTime LastActivityAtUtc);
}
