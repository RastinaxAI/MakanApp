using System.Data;
using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<SendMessageStoreCommand> PrepareEditMessageSafetyAsync(Guid userId, Guid conversationId, Guid messageId,
        AccessContext access, string? text, byte[] expectedVersion, DateTime nowUtc, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId, ct) ?? throw ConversationNotFound();
        var participant = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(conversation, participant, userId, access, ct);
        var message = await dbContext.Messages.AsNoTracking().SingleOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId, ct) ?? throw MessageNotFound();
        if (message.Kind == MessageKind.Poll) throw new MessagingException(MessagingErrorCodes.MessageNotEditable, "تعریف نظرسنجی منتشرشده تغییر نمی‌کند.");
        if (message.SenderUserId != userId) throw new MessagingException(MessagingErrorCodes.MessageNotEditable, "فقط فرستنده می‌تواند ویرایش کند.");
        if (message.IsDeleted) throw new MessagingException(MessagingErrorCodes.MessageDeleted, "پیام حذف شده است.");
        if (!message.RowVersion.SequenceEqual(expectedVersion)) throw MessageConflict();
        if (!message.IsWithinSenderMutationWindow(nowUtc)) throw new MessagingException(MessagingErrorCodes.MessageEditWindowExpired, "مهلت ویرایش پایان یافته است.");
        var attachments = await dbContext.MessageAttachments.Where(a => a.MessageId == messageId).Select(a => a.FileAssetId).ToArrayAsync(ct);
        // شناسه قطعی تلاش ویرایش از شناسه پیام، نسخه و متن؛ ارسال جدید یا بازنویسی history نیست.
        var attemptHash = SHA256.HashData(Encoding.UTF8.GetBytes($"edit:{messageId:N}:{Convert.ToBase64String(expectedVersion)}:{text}"));
        return new SendMessageStoreCommand(new Guid(attemptHash.AsSpan(0, 16)), message.Kind, text, attachments,
            null, [], null, true, message.SharedContactUserId);
    }
    public async Task<MessageSafetyPreparationStoreResult> PrepareMessageSafetyAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        SendMessageStoreCommand command,
        byte[] requestPayloadHash,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        var participant = await dbContext.ConversationParticipants
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.ConversationId == conversationId &&
                item.UserId == userId &&
                item.Status == ConversationParticipantStatus.Active &&
                item.EndedAtUtc == null,
                cancellationToken) ?? throw ConversationNotFound();
        await EnsureSafetyOperationAuthorizedAsync(
            conversation,
            participant,
            userId,
            accessContext,
            command,
            nowUtc,
            cancellationToken);

        var existingMessage = command.SafetyPurpose == MessageSafetyPurpose.Message ? await dbContext.Messages
            .AsNoTracking()
            .SingleOrDefaultAsync(message =>
                message.ConversationId == conversationId &&
                message.SenderUserId == userId &&
                message.ClientMessageId == command.ClientMessageId,
                cancellationToken) : null;
        if (existingMessage is not null)
        {
            await EnsureRetryMatchesAsync(existingMessage, command, cancellationToken);
            return new MessageSafetyPreparationStoreResult(existingMessage, null, false, []);
        }

        if (command.SafetyPurpose == MessageSafetyPurpose.Message)
        {
            await ValidateScheduledSendAsync(userId, conversationId, command, false, cancellationToken, accessContext);
            await EnsureContactCardAudienceAsync(conversation, userId, command, cancellationToken);
            await EnsureTopicOpenAsync(conversation, command.TopicId, cancellationToken);
        }

        var existingIncident = await dbContext.SafetyIncidents
            .AsNoTracking()
            .SingleOrDefaultAsync(incident =>
                incident.ConversationId == conversationId &&
                incident.ActorUserId == userId &&
                incident.ClientMessageId == command.ClientMessageId,
                cancellationToken);
        if (existingIncident is not null)
        {
            EnsureSafetyRetryMatches(existingIncident, requestPayloadHash);
            return new MessageSafetyPreparationStoreResult(
                null,
                existingIncident.Decision,
                false,
                []);
        }

        var files = await LoadAndValidateSafetyFilesAsync(
            conversation,
            userId,
            command,
            cancellationToken);
        var requiresEvaluation = await HasProtectedAudienceAsync(conversation, cancellationToken);
        return new MessageSafetyPreparationStoreResult(
            null,
            null,
            requiresEvaluation,
            files.Select(file => new ContentSafetyAttachment(
                file.Id,
                file.ContentType,
                file.SizeBytes,
                file.Sha256Hash!)).ToArray());
    }

    public async Task<SendMessageStoreCommand> PrepareForwardMessageSafetyAsync(
        Guid userId,
        Guid sourceConversationId,
        Guid sourceMessageId,
        AccessContext accessContext,
        Guid destinationConversationId,
        Guid clientMessageId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var sourceConversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sourceConversationId, cancellationToken)
            ?? throw MessageNotFound();
        var destinationConversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == destinationConversationId, cancellationToken)
            ?? throw ConversationNotFound();
        var sourceParticipant = await dbContext.ConversationParticipants
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.ConversationId == sourceConversationId &&
                item.UserId == userId &&
                item.Status == ConversationParticipantStatus.Active &&
                item.EndedAtUtc == null,
                cancellationToken) ?? throw MessageNotFound();
        var destinationParticipant = await dbContext.ConversationParticipants
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.ConversationId == destinationConversationId &&
                item.UserId == userId &&
                item.Status == ConversationParticipantStatus.Active &&
                item.EndedAtUtc == null,
                cancellationToken) ?? throw ConversationNotFound();

        await EnsureCanReadAdvancedAsync(
            sourceConversation,
            sourceParticipant,
            userId,
            accessContext,
            cancellationToken);
        if (sourceConversation.Scope != destinationConversation.Scope ||
            sourceConversation.OrganizationId != destinationConversation.OrganizationId)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageForwardScopeNotAllowed,
                "انتقال پیام میان محدوده‌های متفاوت بدون مجوز خروج صریح مجاز نیست.");
        }

        var source = await dbContext.Messages
            .AsNoTracking()
            .SingleOrDefaultAsync(message =>
                message.Id == sourceMessageId &&
                message.ConversationId == sourceConversationId,
                cancellationToken) ?? throw MessageNotFound();
        if (source.IsDeleted)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageForwardNotAllowed,
                "پیام حذف‌شده قابل فوروارد نیست.");
        }

        var sourceVisibleFromSequence = await GetVisibleFromSequenceAsync(
            sourceConversationId,
            userId,
            sourceParticipant.HistoryVisibleFromSequence,
            cancellationToken);
        if (source.Sequence < sourceVisibleFromSequence ||
            await dbContext.MessageUserVisibilities.AsNoTracking().AnyAsync(
                hidden => hidden.MessageId == source.Id && hidden.UserId == userId,
                cancellationToken))
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageForwardNotAllowed,
                "پیام مبدأ برای فوروارد در دسترس نیست.");
        }

        var forwardedContent = await BuildForwardContentAsync(source, cancellationToken);
        await EnsureCanSendAdvancedAsync(
            destinationConversation,
            destinationParticipant,
            userId,
            accessContext,
            forwardedContent.Kind,
            nowUtc,
            cancellationToken);
        var attachmentIds = await dbContext.MessageAttachments
            .AsNoTracking()
            .Where(attachment => attachment.MessageId == source.Id)
            .OrderBy(attachment => attachment.FileAssetId)
            .Select(attachment => attachment.FileAssetId)
            .ToArrayAsync(cancellationToken);
        return new SendMessageStoreCommand(
            clientMessageId,
            forwardedContent.Kind,
            forwardedContent.Text,
            attachmentIds,
            null,
            [],
            source.Id,
            true,
            source.SharedContactUserId);
    }

    public async Task<SafetyIncidentStoreResult> RecordSafetyIncidentAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        SendMessageStoreCommand command,
        byte[] requestPayloadHash,
        ContentSafetyResult result,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var conversation = await LockConversationAsync(conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (command.ScheduledClaim is null)
            nowUtc = await EnsureAdvancedSessionAsync(userId, accessContext.SessionId, accessContext, cancellationToken);
        var participant = await LoadActiveParticipantAsync(conversationId, userId, cancellationToken)
            ?? throw ConversationNotFound();
        await EnsureSafetyOperationAuthorizedAsync(
            conversation,
            participant,
            userId,
            accessContext,
            command,
            nowUtc,
            cancellationToken);

        var existingMessage = command.SafetyPurpose == MessageSafetyPurpose.Message ? await dbContext.Messages
            .FromSqlInterpolated($"SELECT * FROM [messaging].[Messages] WITH (UPDLOCK, HOLDLOCK) WHERE [ConversationId] = {conversationId} AND [SenderUserId] = {userId} AND [ClientMessageId] = {command.ClientMessageId}")
            .SingleOrDefaultAsync(cancellationToken) : null;
        if (existingMessage is not null)
        {
            await EnsureRetryMatchesAsync(existingMessage, command, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new SafetyIncidentStoreResult(existingMessage, ContentSafetyDecision.Allowed);
        }

        if (command.SafetyPurpose == MessageSafetyPurpose.Message)
        {
            await ValidateScheduledSendAsync(userId, conversationId, command, true, cancellationToken, accessContext);
            await EnsureContactCardAudienceAsync(conversation, userId, command, cancellationToken);
        }
        var existingIncident = await dbContext.SafetyIncidents
            .FromSqlInterpolated($"SELECT * FROM [messaging].[SafetyIncidents] WITH (UPDLOCK, HOLDLOCK) WHERE [ConversationId] = {conversationId} AND [ActorUserId] = {userId} AND [ClientMessageId] = {command.ClientMessageId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (existingIncident is not null)
        {
            EnsureSafetyRetryMatches(existingIncident, requestPayloadHash);
            await transaction.CommitAsync(cancellationToken);
            return new SafetyIncidentStoreResult(null, existingIncident.Decision);
        }

        var files = await LoadAndValidateSafetyFilesAsync(
            conversation,
            userId,
            command,
            cancellationToken);
        var policyCodes = string.Join(
            ',',
            (result.PolicyCodes ?? [])
                .Select(code => code.Trim())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
        var incident = SafetyIncident.Create(
            conversationId,
            userId,
            command.ClientMessageId,
            command.Kind,
            result.Decision,
            result.RiskLevel,
            result.ProviderReference,
            policyCodes,
            command.Text,
            HashSafetyContent(command.Text, files.Select(file => file.Sha256Hash!)),
            requestPayloadHash,
            nowUtc);
        var moderationCase = ModerationCase.CreateForSafetyIncident(
            conversationId,
            incident.Id,
            nowUtc);
        dbContext.SafetyIncidents.Add(incident);
        dbContext.ModerationCases.Add(moderationCase);
        dbContext.ModerationActions.Add(ModerationAction.Create(
            moderationCase.Id,
            ModerationActionType.Created,
            userId,
            nowUtc));
        foreach (var file in files)
        {
            file.Retain(nowUtc);
            dbContext.ModerationEvidenceAttachments.Add(
                ModerationEvidenceAttachment.Create(moderationCase.Id, file.Id, nowUtc));
        }

        await SaveSafetyAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SafetyIncidentStoreResult(null, incident.Decision);
    }

    private async Task<MakanApp.Domain.Storage.FileAsset[]> LoadAndValidateSafetyFilesAsync(
        Conversation conversation,
        Guid userId,
        SendMessageStoreCommand command,
        CancellationToken cancellationToken)
    {
        var attachmentIds = command.AttachmentIds.Distinct().ToArray();
        var files = attachmentIds.Length == 0
            ? []
            : await dbContext.FileAssets
                .Where(file => attachmentIds.Contains(file.Id))
                .ToArrayAsync(cancellationToken);
        if (files.Length != attachmentIds.Length)
        {
            throw AttachmentNotAllowed();
        }

        foreach (var file in files)
        {
            EnsureFileCanBeAttached(
                file,
                conversation,
                userId,
                command.Kind,
                command.AllowSourceAttachments);
        }

        return files;
    }

    private async Task EnsureSafetyGateSatisfiedAsync(
        Conversation conversation,
        bool safetyEvaluated,
        CancellationToken cancellationToken)
    {
        if (safetyEvaluated)
        {
            return;
        }

        var hasNonAdultParticipant = await HasProtectedAudienceAsync(conversation, cancellationToken);
        if (hasNonAdultParticipant)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageSafetyUnavailable,
                "بررسی ایمنی پیام موقتاً ممکن نیست؛ دوباره تلاش کنید.");
        }
    }

    private async Task<bool> HasProtectedAudienceAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        if (conversation.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic)
            return await (from member in MessengerRelationshipQuery.ClassMembers(dbContext)
                join user in dbContext.Users on member.UserId equals user.Id
                where member.ClassId == conversation.AcademicClassId && member.OrganizationId == conversation.OrganizationId &&
                    (user.CommunicationAgeCategory != CommunicationAgeCategory.Adult ||
                     user.CommunicationAgeVerificationStatus != CommunicationAgeVerificationStatus.Verified)
                select member.UserId).AnyAsync(cancellationToken);
        return await (
            from candidate in dbContext.ConversationParticipants
            join account in dbContext.Users on candidate.UserId equals account.Id
            where candidate.ConversationId == conversation.Id &&
                  candidate.Status == ConversationParticipantStatus.Active &&
                  candidate.EndedAtUtc == null &&
                  (account.CommunicationAgeCategory != CommunicationAgeCategory.Adult ||
                   account.CommunicationAgeVerificationStatus != CommunicationAgeVerificationStatus.Verified)
            select candidate.Id)
            .AnyAsync(cancellationToken);
    }

    private static void EnsureSafetyRetryMatches(
        SafetyIncident incident,
        byte[] requestPayloadHash)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                incident.RequestPayloadHash,
                requestPayloadHash))
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageIdempotencyConflict,
                "این شناسه پیام قبلاً برای محتوای دیگری استفاده شده است.");
        }
    }

    private static byte[] HashSafetyContent(
        string? text,
        IEnumerable<string> attachmentHashes)
    {
        var canonical = string.Join('\n', new[] { text ?? string.Empty }
            .Concat(attachmentHashes.Order(StringComparer.Ordinal)));
        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
    }
}
