using System.Data;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<Message> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        SendMessageStoreCommand command,
        bool safetyEvaluated,
        int maximumTextLength,
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
        await EnsureCanReadAdvancedAsync(
            conversation,
            participant,
            userId,
            accessContext,
            cancellationToken);
        await EnsureActorCanMessageAsync(userId, nowUtc, cancellationToken);
        var existing = await dbContext.Messages
            .FromSqlInterpolated($"SELECT * FROM [messaging].[Messages] WITH (UPDLOCK, HOLDLOCK) WHERE [ConversationId] = {conversation.Id} AND [SenderUserId] = {userId} AND [ClientMessageId] = {command.ClientMessageId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            await EnsureRetryMatchesAsync(existing, command, cancellationToken);
            if (command.ScheduledClaim is not null)
            {
                var recovered = await ValidateScheduledSendAsync(userId, conversationId, command, true, cancellationToken, accessContext) ?? throw ScheduledClaimLost();
                recovered.Dispatch(command.ScheduledClaim.Token, existing.Id, timeProvider.GetUtcNow().UtcDateTime);
                await SaveScheduledAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        var scheduled = command.ScheduledClaim is null ? null : await ValidateScheduledSendAsync(userId, conversationId, command, true, cancellationToken, accessContext);
        await EnsureCanSendAdvancedAsync(
            conversation,
            participant,
            userId,
            accessContext,
            command.Kind,
            nowUtc,
            cancellationToken);
        await EnsureSafetyGateSatisfiedAsync(
            conversation,
            safetyEvaluated,
            cancellationToken);
        var created = await CreateMessageAsync(
            conversation,
            participant,
            userId,
            command,
            maximumTextLength,
            nowUtc,
            cancellationToken);
        if (scheduled is not null) scheduled.Dispatch(command.ScheduledClaim!.Token, created.Message.Id, timeProvider.GetUtcNow().UtcDateTime);
        await SaveAdvancedAsync(cancellationToken);
        await EnqueueMessageNotificationsAsync(
            conversation,
            created.Message,
            userId,
            nowUtc,
            cancellationToken);
        await AppendChangeAsync(
            conversation,
            MessagingChangeType.MessageCreated,
            created.Message.Id,
            VersionOf(created.Message.RowVersion),
            nowUtc,
            userId,
            null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        outboxWakeSignal.Signal();
        return created.Message;
    }

    public async Task<Message> ForwardMessageAsync(
        Guid userId,
        Guid sourceConversationId,
        Guid sourceMessageId,
        AccessContext accessContext,
        Guid destinationConversationId,
        SendMessageStoreCommand command,
        bool safetyEvaluated,
        int maximumTextLength,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var conversations = await LockConversationsAsync(
            sourceConversationId,
            destinationConversationId,
            cancellationToken);
        nowUtc = await EnsureAdvancedSessionAsync(userId, accessContext.SessionId, accessContext, cancellationToken);
        var sourceConversation = conversations.SingleOrDefault(item => item.Id == sourceConversationId)
            ?? throw MessageNotFound();
        var destinationConversation = conversations.SingleOrDefault(item => item.Id == destinationConversationId)
            ?? throw ConversationNotFound();
        var sourceParticipant = await LoadActiveParticipantAsync(
            sourceConversationId,
            userId,
            cancellationToken) ?? throw MessageNotFound();
        var destinationParticipant = await LoadActiveParticipantAsync(
            destinationConversationId,
            userId,
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

        var source = await LockMessageAsync(
            sourceConversationId,
            sourceMessageId,
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
            await dbContext.MessageUserVisibilities.AnyAsync(
                hidden => hidden.MessageId == source.Id && hidden.UserId == userId,
                cancellationToken))
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageForwardNotAllowed,
                "پیام مبدا برای فوروارد در دسترس نیست.");
        }

        await EnsureCanSendAdvancedAsync(
            destinationConversation,
            destinationParticipant,
            userId,
            accessContext,
            source.Kind,
            nowUtc,
            cancellationToken);
        await EnsureSafetyGateSatisfiedAsync(
            destinationConversation,
            safetyEvaluated,
            cancellationToken);

        var attachmentIds = await dbContext.MessageAttachments
            .Where(attachment => attachment.MessageId == source.Id)
            .OrderBy(attachment => attachment.FileAssetId)
            .Select(attachment => attachment.FileAssetId)
            .ToArrayAsync(cancellationToken);
        EnsureForwardPreparationStillMatches(source, attachmentIds, command);
        var forwarded = await CreateMessageAsync(
            destinationConversation,
            destinationParticipant,
            userId,
            command,
            maximumTextLength,
            nowUtc,
            cancellationToken);
        if (forwarded.Created)
        {
            await SaveAdvancedAsync(cancellationToken);
            await EnqueueMessageNotificationsAsync(
                destinationConversation,
                forwarded.Message,
                userId,
                nowUtc,
                cancellationToken);
            await AppendChangeAsync(
                destinationConversation,
                MessagingChangeType.MessageCreated,
                forwarded.Message.Id,
                VersionOf(forwarded.Message.RowVersion),
                nowUtc,
                userId,
                null,
                cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        if (forwarded.Created)
        {
            outboxWakeSignal.Signal();
        }

        return forwarded.Message;
    }

    private static void EnsureForwardPreparationStillMatches(
        Message source,
        IReadOnlyCollection<Guid> attachmentIds,
        SendMessageStoreCommand command)
    {
        if (command.ForwardedFromMessageId != source.Id ||
            command.Kind != source.Kind ||
            !string.Equals(command.Text, source.Text, StringComparison.Ordinal) ||
            command.ReplyToMessageId.HasValue ||
            command.MentionedUserIds.Count != 0 ||
            !command.AllowSourceAttachments ||
            command.ContactUserId != source.SharedContactUserId ||
            !attachmentIds.Order().SequenceEqual(command.AttachmentIds.Order()))
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageSafetyUnavailable,
                "محتوای پیام مبدأ هنگام بررسی تغییر کرد؛ دوباره تلاش کنید.");
        }
    }

    private async Task<(Message Message, bool Created)> CreateMessageAsync(
        Conversation conversation,
        ConversationParticipant participant,
        Guid userId,
        SendMessageStoreCommand command,
        int maximumTextLength,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Messages
            .FromSqlInterpolated($"SELECT * FROM [messaging].[Messages] WITH (UPDLOCK, HOLDLOCK) WHERE [ConversationId] = {conversation.Id} AND [SenderUserId] = {userId} AND [ClientMessageId] = {command.ClientMessageId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            await EnsureRetryMatchesAsync(existing, command, cancellationToken);
            return (existing, false);
        }

        var (files, mentionedUserIds) = await LoadAndValidateMessageReferencesAsync(conversation, participant, userId, command, cancellationToken);
        if (command.ScheduledClaim is null) await ValidateScheduledSendAsync(userId, conversation.Id, command, true, cancellationToken);

        Message message;
        try
        {
            message = Message.Create(
                conversation.Id,
                participant.Id,
                userId,
                command.ClientMessageId,
                conversation.AllocateNextMessageSequence(),
                command.Kind,
                command.Text,
                files.Length,
                command.ReplyToMessageId,
                command.ForwardedFromMessageId,
                maximumTextLength,
                nowUtc,
                command.TopicId,
                command.IsSilent);
            if (command.Kind == MessageKind.ContactCard && command.ContactUserId.HasValue)
            {
                var identity = await LoadSafeIdentityAsync(command.ContactUserId.Value, cancellationToken);
                message.SetContactCardSnapshot(
                    identity.UserId,
                    identity.Username,
                    identity.DisplayName);
            }
        }
        catch (ArgumentException exception)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageKindInvalid,
                "ترکیب نوع، متن و پیوست پیام معتبر نیست.",
                exception);
        }

        dbContext.Messages.Add(message);
        dbContext.MessageRevisions.Add(MessageRevision.Create(
            message.Id,
            message.CurrentRevisionNumber,
            message.Text,
            userId,
            nowUtc));
        foreach (var file in files)
        {
            dbContext.MessageAttachments.Add(MessageAttachment.Create(
                message.Id,
                file.Id,
                command.Kind,
                nowUtc));
            file.Retain(nowUtc);
        }

        foreach (var mentionedUserId in mentionedUserIds)
        {
            dbContext.MessageMentions.Add(MessageMention.Create(
                message.Id,
                mentionedUserId,
                nowUtc));
        }

        return (message, true);
    }

    private async Task<(FileAsset[] Files, Guid[] MentionedUserIds)> LoadAndValidateMessageReferencesAsync(
        Conversation conversation, ConversationParticipant participant, Guid userId, SendMessageStoreCommand command, CancellationToken cancellationToken)
    {
        await EnsureContactCardAudienceAsync(conversation, userId, command, cancellationToken);
        if (command.ReplyToMessageId.HasValue)
        {
            var visibleFromSequence = await GetVisibleFromSequenceAsync(
                conversation.Id,
                userId,
                participant.HistoryVisibleFromSequence,
                cancellationToken);
            var replyExists = await dbContext.Messages.AnyAsync(
                message => message.Id == command.ReplyToMessageId.Value &&
                           message.ConversationId == conversation.Id &&
                           message.Sequence >= visibleFromSequence &&
                           !dbContext.MessageUserVisibilities.Any(hidden =>
                               hidden.MessageId == message.Id && hidden.UserId == userId),
                cancellationToken);
            if (!replyExists)
            {
                throw new MessagingException(
                    MessagingErrorCodes.MessageReplyNotAllowed,
                    "پیام مرجع پاسخ در این گفتگو در دسترس نیست.");
            }
        }

        var mentionedUserIds = command.MentionedUserIds.Distinct().ToArray();
        if (command.SafetyPurpose != MessageSafetyPurpose.Message) throw new MessagingException(MessagingErrorCodes.MessageKindInvalid, "محتوای مدیریتی پیام نیست.");
        await EnsureTopicOpenAsync(conversation, command.TopicId, cancellationToken);
        if (mentionedUserIds.Length > 0)
        {
            var eligibleMentionCount = await dbContext.ConversationParticipants.CountAsync(
                candidate => candidate.ConversationId == conversation.Id &&
                             mentionedUserIds.Contains(candidate.UserId) &&
                             candidate.Status == ConversationParticipantStatus.Active &&
                             candidate.EndedAtUtc == null,
                cancellationToken);
            if (eligibleMentionCount != mentionedUserIds.Length)
            {
                throw new MessagingException(
                    MessagingErrorCodes.MentionNotAllowed,
                    "یکی از کاربران منشن‌شده عضو قابل مشاهده این گفتگو نیست.");
            }
        }

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
        return (files, mentionedUserIds);
    }

    private async Task EnsureRetryMatchesAsync(
        Message existing,
        SendMessageStoreCommand command,
        CancellationToken cancellationToken)
    {
        if (existing.IsDeleted)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageDeleted,
                "پیام متناظر با این شناسه قبلاً حذف شده است.");
        }

        var attachments = await dbContext.MessageAttachments
            .Where(item => item.MessageId == existing.Id)
            .OrderBy(item => item.FileAssetId)
            .Select(item => item.FileAssetId)
            .ToArrayAsync(cancellationToken);
        var mentions = await dbContext.MessageMentions
            .Where(item => item.MessageId == existing.Id)
            .OrderBy(item => item.MentionedUserId)
            .Select(item => item.MentionedUserId)
            .ToArrayAsync(cancellationToken);
        if (existing.Kind != command.Kind ||
            !string.Equals(existing.Text, command.Text, StringComparison.Ordinal) ||
            existing.ReplyToMessageId != command.ReplyToMessageId ||
            existing.ForwardedFromMessageId != command.ForwardedFromMessageId ||
            existing.SharedContactUserId != command.ContactUserId ||
            existing.TopicId != command.TopicId ||
            existing.IsSilent != command.IsSilent ||
            !attachments.SequenceEqual(command.AttachmentIds.Distinct().Order()) ||
            !mentions.SequenceEqual(command.MentionedUserIds.Distinct().Order()))
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageIdempotencyConflict,
                "این شناسه پیام قبلاً برای محتوای دیگری استفاده شده است.");
        }
    }

    private static void EnsureFileCanBeAttached(
        FileAsset file,
        Conversation conversation,
        Guid userId,
        MessageKind kind,
        bool allowSourceAttachment)
    {
        if (file.Status != FileAssetStatus.Ready)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageAttachmentNotReady,
                "فایل پیوست هنوز آماده استفاده نیست.");
        }

        if (file.OrganizationId != conversation.OrganizationId ||
            !allowSourceAttachment && file.UploadedByUserId != userId)
        {
            throw AttachmentNotAllowed();
        }

        var contentTypeMatches = kind switch
        {
            MessageKind.Image => file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase),
            MessageKind.Video => file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase),
            MessageKind.Voice => file.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase),
            MessageKind.File => true,
            _ => false
        };
        if (!contentTypeMatches)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageKindInvalid,
                "نوع رسانه با فراداده معتبر فایل سازگار نیست.");
        }
    }

    private async Task EnsureCanSendAdvancedAsync(
        Conversation conversation,
        ConversationParticipant participant,
        Guid userId,
        AccessContext accessContext,
        MessageKind messageKind,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await EnsureActorCanMessageAsync(userId, nowUtc, cancellationToken);
        await EnsureCanReadAdvancedAsync(
            conversation,
            participant,
            userId,
            accessContext,
            cancellationToken);
        if (conversation.Status != ConversationStatus.Active)
        {
            throw new MessagingException(
                MessagingErrorCodes.ConversationArchived,
                "گفتگوی بایگانی‌شده پیام تازه نمی‌پذیرد.");
        }

        if (conversation.Type is ConversationType.Group or ConversationType.Channel)
        {
            await EnsureCapabilityNotRestrictedAsync(conversation.Id, userId,
                messageKind == MessageKind.Text ? GroupMemberCapability.SendMessages : GroupMemberCapability.SendMedia, nowUtc, cancellationToken);
            if (conversation.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic && dbContext.Database.CurrentTransaction is not null)
            {
                var members = await MessengerRelationshipQuery.ClassMembers(dbContext).Where(m => m.ClassId == conversation.AcademicClassId &&
                    m.OrganizationId == conversation.OrganizationId).Select(m => m.UserId).Distinct().ToArrayAsync(cancellationToken);
                await SynchronizeOfficialParticipantsAsync(conversation, userId, members, nowUtc, cancellationToken);
            }
            await EnsurePersonalAudienceSafetyAsync(conversation, userId, nowUtc, cancellationToken);
            if (participant.Role == ConversationParticipantRole.Owner)
                await EnsurePersonalCreationSafetyAsync(userId, conversation.Scope, conversation.Type, conversation.Visibility, cancellationToken);
            if (conversation.Scope == ConversationScope.Personal && conversation.Type == ConversationType.Channel &&
                !await IsVerifiedAdultAsync(userId, cancellationToken)) throw ManagementNotAllowed();
            var canPublish = conversation.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic &&
                conversation.AcademicClassId.HasValue && await MessengerRelationshipQuery.ClassMembers(dbContext).AnyAsync(m =>
                    m.UserId == userId && m.ClassId == conversation.AcademicClassId && m.OrganizationId == conversation.OrganizationId, cancellationToken) ||
                conversation.ManagementPolicy == ConversationManagementPolicy.UserManaged &&
                (conversation.Type == ConversationType.Channel
                    ? participant.Role == ConversationParticipantRole.Owner ||
                      participant.Role == ConversationParticipantRole.Admin &&
                      participant.AdminPermissions.HasFlag(ConversationAdminPermission.PublishMessages)
                    : participant.Role == ConversationParticipantRole.Owner ||
                      (messageKind == MessageKind.Text
                          ? conversation.MemberCapabilities.HasFlag(GroupMemberCapability.SendMessages)
                          : conversation.MemberCapabilities.HasFlag(GroupMemberCapability.SendMedia)));
            if (!canPublish)
            {
                throw new MessagingException(
                    MessagingErrorCodes.MessagePublishNotAllowed,
                    "نقش فعلی اجازه انتشار پیام در این گفتگو را ندارد.");
            }

            return;
        }

        if (conversation.Type == ConversationType.Saved)
        {
            if (conversation.SavedOwnerUserId != userId)
            {
                throw new MessagingException(
                    MessagingErrorCodes.MessagePublishNotAllowed,
                    "انتشار پیام در این گفتگو مجاز نیست.");
            }

            return;
        }

        if (conversation.Type != ConversationType.Direct)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessagePublishNotAllowed,
                "انتشار پیام در این نوع گفتگو مجاز نیست.");
        }

        var targetUserId = conversation.GetDirectPair().Other(userId);
        if (await HasEffectiveUserBlockAsync(userId, targetUserId, cancellationToken))
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageNotAllowed,
                "رابطه ارتباطی معتبر برای ارسال پیام وجود ندارد.");
        }

        var facts = await LoadEligibilityFactsAsync(
            userId,
            targetUserId,
            conversation.Scope,
            conversation.OrganizationId,
            accessContext,
            nowUtc,
            cancellationToken);
        if (!eligibilityPolicy.CanStartOrSend(facts, MessagingEligibilityOperation.Send))
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageNotAllowed,
                "رابطه ارتباطی معتبر برای ارسال پیام وجود ندارد.");
        }
    }

    private async Task EnsureCanReadAdvancedAsync(
        Conversation conversation,
        ConversationParticipant participant,
        Guid userId,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        if (!participant.IsActive ||
            conversation.Type == ConversationType.Saved && conversation.SavedOwnerUserId != userId ||
            !await CanAccessScopeAsync(conversation, userId, accessContext, cancellationToken))
        {
            throw ConversationNotFound();
        }
    }

    private Task<Message?> LockMessageAsync(
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken) =>
        dbContext.Messages
            .FromSqlInterpolated($"SELECT * FROM [messaging].[Messages] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {messageId} AND [ConversationId] = {conversationId}")
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<Conversation[]> LockConversationsAsync(
        Guid firstConversationId,
        Guid secondConversationId,
        CancellationToken cancellationToken)
    {
        if (firstConversationId == secondConversationId)
        {
            var single = await LockConversationAsync(firstConversationId, cancellationToken);
            return single is null ? [] : [single];
        }

        var low = firstConversationId.CompareTo(secondConversationId) < 0
            ? firstConversationId
            : secondConversationId;
        var high = low == firstConversationId ? secondConversationId : firstConversationId;
        return await dbContext.Conversations
            .FromSqlInterpolated($"SELECT * FROM [messaging].[Conversations] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {low} OR [Id] = {high} ORDER BY [Id]")
            .ToArrayAsync(cancellationToken);
    }

    private async Task SaveAdvancedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw MessageConflict(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            throw MessageConflict(exception);
        }
    }

    private static MessagingException AttachmentNotAllowed() =>
        new(
            MessagingErrorCodes.MessageAttachmentNotAllowed,
            "استفاده از این فایل در پیام مجاز نیست.");

    private static MessagingException MessageNotFound() =>
        new(MessagingErrorCodes.MessageNotFound, "پیام یافت نشد.");

    private static MessagingException MessageConflict(Exception? exception = null) =>
        new(
            MessagingErrorCodes.MessageEditConflict,
            "پیام هم‌زمان تغییر کرده است؛ نسخه تازه را دریافت کنید.",
            exception);
}
