using System.Data;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore : IMessagingPresentationStore
{
    public async Task<IReadOnlyCollection<ChatFolderResult>> GetFoldersAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await LockPresentationOwnerAsync(userId, cancellationToken);
        var folders = await dbContext.ChatFolders.AsNoTracking().Where(f => f.OwnerUserId == userId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Id).Take(ChatFolder.MaximumFoldersPerUser).ToArrayAsync(cancellationToken);
        var ids = folders.Select(f => f.Id).ToArray();
        var overrides = await dbContext.FolderConversationOverrides.AsNoTracking().Where(o => ids.Contains(o.FolderId))
            .ToArrayAsync(cancellationToken);
        var result = folders.Select(f => MapFolder(f, overrides.Where(o => o.FolderId == f.Id))).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<ChatFolderResult> SaveFolderAsync(Guid userId, Guid folderId, SaveChatFolderCommand command,
        byte[]? expectedVersion, AccessContext context, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // قفل مالک، سقف تعداد پوشه و ساخت هم‌زمان دو دستگاه را سریال می‌کند؛ هیچ فیلد Identity تغییر نمی‌کند.
        await LockPresentationOwnerAsync(userId, cancellationToken);
        await EnsureAdvancedSessionAsync(userId, context.SessionId, context, cancellationToken);
        var folder = await dbContext.ChatFolders.SingleOrDefaultAsync(f => f.Id == folderId && f.OwnerUserId == userId, cancellationToken);
        if (folder is null)
        {
            if (expectedVersion is not null || await dbContext.ChatFolders.AnyAsync(f => f.Id == folderId, cancellationToken)) throw PresentationNotFound();
            if (await dbContext.ChatFolders.CountAsync(f => f.OwnerUserId == userId, cancellationToken) >= ChatFolder.MaximumFoldersPerUser)
                throw new MessagingException(MessagingErrorCodes.PresentationInvalid, "سقف پوشه‌ها پر شده است.");
            folder = ChatFolder.Create(folderId, userId); dbContext.ChatFolders.Add(folder);
        }
        else
        {
            if (expectedVersion is null || !folder.RowVersion.SequenceEqual(expectedVersion)) throw PresentationConflict();
            dbContext.Entry(folder).Property(f => f.RowVersion).OriginalValue = expectedVersion;
        }
        await AuthorizeFolderOverridesAsync(userId, command.Overrides.Select(o => o.ConversationId).ToArray(), context, cancellationToken);
        folder.Update(command.Name, command.SortOrder, command.IncludedTypes, command.ExcludedTypes, command.UnreadOnly);
        var existing = await dbContext.FolderConversationOverrides.Where(o => o.FolderId == folderId).ToArrayAsync(cancellationToken);
        dbContext.FolderConversationOverrides.RemoveRange(existing);
        // DELETE و INSERT کلید یکسان در همان Save، identity-map را درگیر می‌کند؛ هر دو داخل همین transaction هستند.
        await SavePresentationAsync(cancellationToken);
        var replacements = command.Overrides.Select(o => FolderConversationOverride.Create(folderId, o.ConversationId, o.Included, o.PinOrder)).ToArray();
        dbContext.FolderConversationOverrides.AddRange(replacements);
        await SavePresentationAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return MapFolder(folder, replacements);
    }

    public async Task DeleteFolderAsync(Guid userId, Guid folderId, byte[] expectedVersion, AccessContext context, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await LockPresentationOwnerAsync(userId, cancellationToken);
        await EnsureAdvancedSessionAsync(userId, context.SessionId, context, cancellationToken);
        var folder = await dbContext.ChatFolders.SingleOrDefaultAsync(f => f.Id == folderId && f.OwnerUserId == userId, cancellationToken)
            ?? throw PresentationNotFound();
        if (!folder.RowVersion.SequenceEqual(expectedVersion)) throw PresentationConflict();
        dbContext.Entry(folder).Property(f => f.RowVersion).OriginalValue = expectedVersion;
        dbContext.ChatFolders.Remove(folder); await SavePresentationAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<SavedMessageLabelsResult> GetSavedLabelsAsync(Guid userId, Guid messageId, AccessContext context, CancellationToken cancellationToken)
    {
        var authorized = await LoadSavedPresentationMessageAsync(userId, messageId, context, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await LockConversationAsync(authorized.Id, cancellationToken);
        await LoadSavedPresentationMessageAsync(userId, messageId, context, cancellationToken);
        var organization = await dbContext.SavedMessageOrganizations.AsNoTracking().SingleOrDefaultAsync(o => o.MessageId == messageId && o.OwnerUserId == userId, cancellationToken);
        var labels = await dbContext.SavedMessageLabels.AsNoTracking().Where(l => l.MessageId == messageId).OrderBy(l => l.Name)
            .Select(l => l.Name).Take(SavedMessageLabel.MaximumLabels).ToArrayAsync(cancellationToken);
        var result = new SavedMessageLabelsResult(messageId, labels, organization is null ? null : Convert.ToBase64String(organization.RowVersion));
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<SavedMessageLabelsResult> ReplaceSavedLabelsAsync(Guid userId, Guid messageId, string[] labels,
        byte[]? expectedVersion, AccessContext context, CancellationToken cancellationToken)
    {
        var authorized = await LoadSavedPresentationMessageAsync(userId, messageId, context, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await LockConversationAsync(authorized.Id, cancellationToken);
        await EnsureAdvancedSessionAsync(userId, context.SessionId, context, cancellationToken);
        await LoadSavedPresentationMessageAsync(userId, messageId, context, cancellationToken);
        var organization = await dbContext.SavedMessageOrganizations.SingleOrDefaultAsync(o => o.MessageId == messageId, cancellationToken);
        if (organization is null)
        {
            if (expectedVersion is not null) throw PresentationConflict();
            organization = SavedMessageOrganization.Create(messageId, userId); dbContext.SavedMessageOrganizations.Add(organization);
        }
        else
        {
            if (organization.OwnerUserId != userId) throw MessageNotFound();
            if (expectedVersion is null || !organization.RowVersion.SequenceEqual(expectedVersion)) throw PresentationConflict();
            dbContext.Entry(organization).Property(o => o.RowVersion).OriginalValue = expectedVersion;
        }
        organization.Touch();
        var existing = await dbContext.SavedMessageLabels.Where(l => l.MessageId == messageId).ToArrayAsync(cancellationToken);
        dbContext.SavedMessageLabels.RemoveRange(existing); await SavePresentationAsync(cancellationToken);
        dbContext.SavedMessageLabels.AddRange(labels.Select(l => SavedMessageLabel.Create(messageId, l)));
        await SavePresentationAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new(messageId, labels, Convert.ToBase64String(organization.RowVersion));
    }

    private async Task<Conversation> LoadSavedPresentationMessageAsync(Guid userId, Guid messageId, AccessContext context, CancellationToken cancellationToken)
    {
        var identity = await dbContext.Messages.AsNoTracking().SingleOrDefaultAsync(m => m.Id == messageId && m.DeletedAtUtc == null, cancellationToken)
            ?? throw MessageNotFound();
        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == identity.ConversationId && c.Type == ConversationType.Saved && c.SavedOwnerUserId == userId, cancellationToken)
            ?? throw MessageNotFound();
        var participant = await LoadActiveParticipantAsync(conversation.Id, userId, cancellationToken) ?? throw MessageNotFound();
        await EnsureCanReadAdvancedAsync(conversation, participant, userId, context, cancellationToken);
        var visibleFrom = await GetVisibleFromSequenceAsync(conversation.Id, userId, participant.HistoryVisibleFromSequence, cancellationToken);
        if (identity.Sequence < visibleFrom || await dbContext.MessageUserVisibilities.AnyAsync(v => v.MessageId == messageId && v.UserId == userId, cancellationToken)) throw MessageNotFound();
        return conversation;
    }

    private async Task LockPresentationOwnerAsync(Guid userId, CancellationToken ct)
    {
        await dbContext.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={userId}").AsNoTracking().SingleAsync(ct);
    }

    private static ChatFolderResult MapFolder(ChatFolder folder, IEnumerable<FolderConversationOverride> overrides) =>
        new(folder.Id, folder.Name, folder.SortOrder, folder.IncludedTypes, folder.ExcludedTypes, folder.UnreadOnly,
            overrides.OrderBy(o => o.ConversationId).Select(o => new FolderOverrideCommand(o.ConversationId, o.Included, o.PinOrder)).ToArray(), Convert.ToBase64String(folder.RowVersion));

    private async Task AuthorizeFolderOverridesAsync(Guid userId, Guid[] conversationIds, AccessContext context, CancellationToken cancellationToken)
    {
        if (conversationIds.Length == 0) return;
        var officialMembers = OfficialSpaceAccessQuery.Conversations(dbContext);
        // همان مرز خواندن فهرست گفتگوها، قبل از mutation و به‌صورت batch؛ انتخاب پوشه مجوز تازه نیست.
        var allowed = await (from c in dbContext.Conversations
            join p in dbContext.ConversationParticipants on c.Id equals p.ConversationId
            where conversationIds.Contains(c.Id) && p.UserId == userId && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null &&
                (c.Type != ConversationType.Saved || c.SavedOwnerUserId == userId) &&
                (c.ManagementPolicy != ConversationManagementPolicy.SystemManagedAcademic ||
                    officialMembers.Any(m => m.UserId == userId && m.ConversationId == c.Id)) &&
                (context.WorkspaceType == WorkspaceType.Personal && c.Scope == ConversationScope.Personal && context.OrganizationId == null ||
                 context.WorkspaceType == WorkspaceType.Organization && c.Scope == ConversationScope.Organization && c.OrganizationId == context.OrganizationId &&
                    dbContext.Memberships.Any(m => m.Id == context.MembershipId && m.UserId == userId && m.OrganizationId == c.OrganizationId &&
                        m.Status == MembershipStatus.Active && m.EndedAtUtc == null))
            select c.Id).Distinct().CountAsync(cancellationToken);
        if (allowed != conversationIds.Length) throw ConversationNotFound();
    }

    private async Task SavePresentationAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException ex) { throw new MessagingException(MessagingErrorCodes.PresentationConflict, "سازمان‌دهی از دستگاه دیگری تغییر کرده است.", ex); }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex)) { throw new MessagingException(MessagingErrorCodes.PresentationConflict, "انتخاب تکراری یا تغییر هم‌زمان است.", ex); }
    }
    private static MessagingException PresentationNotFound() => new(MessagingErrorCodes.PresentationNotFound, "سازمان‌دهی در دسترس نیست.");
    private static MessagingException PresentationConflict() => new(MessagingErrorCodes.PresentationConflict, "نسخهٔ سازمان‌دهی تغییر کرده است.");
}
