using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed class ConversationManagementService(
    IAccessContextResolver accessContextResolver,
    IConversationManagementStore store,
    TimeProvider timeProvider) : IConversationManagementService
{
    public Task<ManagedConversationResult> CreateGroupAsync(
        Guid userId,
        Guid sessionId,
        CreateManagedConversationCommand command,
        CancellationToken cancellationToken) =>
        CreateAsync(userId, sessionId, ConversationType.Group, command, cancellationToken);

    public Task<ManagedConversationResult> CreateChannelAsync(
        Guid userId,
        Guid sessionId,
        CreateManagedConversationCommand command,
        CancellationToken cancellationToken) =>
        CreateAsync(userId, sessionId, ConversationType.Channel, command, cancellationToken);

    public async Task<ManagedConversationResult> GetDetailsAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        EnsureConversationId(conversationId);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.GetDetailsAsync(userId, conversationId, context, cancellationToken));
    }

    public async Task<ConversationParticipantPageResult> GetMembersAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureConversationId(conversationId);
        if (page is <= 0 or > 100_000 || pageSize is <= 0 or > 100)
        {
            throw Error(MessagingErrorCodes.MessagePageInvalid, "صفحه‌بندی اعضای گفتگو معتبر نیست.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var result = await store.GetMembersAsync(
            userId,
            conversationId,
            page,
            pageSize,
            context,
            cancellationToken);
        return new ConversationParticipantPageResult(
            result.Items.Select(MapParticipant).ToArray(),
            page,
            pageSize,
            result.HasMore);
    }

    public async Task<ManagedConversationResult> AddMemberAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        AddConversationMemberCommand command,
        CancellationToken cancellationToken)
    {
        EnsureConversationAndTarget(conversationId, command.UserId);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.AddMemberAsync(
            userId,
            conversationId,
            command.UserId,
            context,
            UtcNow(),
            cancellationToken));
    }

    public async Task<ManagedConversationResult> RemoveMemberAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        EnsureConversationAndTarget(conversationId, targetUserId);
        if (targetUserId == userId)
        {
            throw Error(MessagingErrorCodes.ConversationManagementNotAllowed, "برای خروج خودتان از endpoint خروج استفاده کنید.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.RemoveMemberAsync(
            userId,
            conversationId,
            targetUserId,
            context,
            UtcNow(),
            cancellationToken));
    }

    public async Task<ManagedConversationResult> LeaveAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        EnsureConversationId(conversationId);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.LeaveAsync(
            userId,
            conversationId,
            context,
            UtcNow(),
            cancellationToken));
    }

    public async Task<ManagedConversationResult> ChangeMemberRoleAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid targetUserId,
        ChangeConversationMemberRoleCommand command,
        CancellationToken cancellationToken)
    {
        EnsureConversationAndTarget(conversationId, targetUserId);
        if (command.Role is not ConversationParticipantRole.Admin and not ConversationParticipantRole.Member)
        {
            throw Error(MessagingErrorCodes.ConversationRoleInvalid, "نقش درخواستی برای این عملیات معتبر نیست.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.ChangeMemberRoleAsync(
            userId,
            conversationId,
            targetUserId,
            command.Role,
            command.AdminPermissions,
            context,
            UtcNow(),
            cancellationToken));
    }

    public async Task<ManagedConversationResult> UpdateMemberPermissionsAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid targetUserId,
        UpdateConversationPermissionsCommand command,
        CancellationToken cancellationToken)
    {
        EnsureConversationAndTarget(conversationId, targetUserId);
        if ((command.Permissions & ~ConversationAdminPermission.All) != 0)
        {
            throw Error(MessagingErrorCodes.ConversationPermissionRequired, "مجموعه مجوزها معتبر نیست.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.UpdateMemberPermissionsAsync(
            userId,
            conversationId,
            targetUserId,
            command.Permissions,
            DecodeVersion(command.ExpectedVersion),
            context,
            UtcNow(),
            cancellationToken));
    }

    public async Task<ManagedConversationResult> UpdateSettingsAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        UpdateConversationSettingsCommand command,
        CancellationToken cancellationToken)
    {
        EnsureConversationId(conversationId);
        if (!Enum.IsDefined(command.Visibility) || !Enum.IsDefined(command.GroupHistoryPolicy) ||
            (command.MemberCapabilities & ~GroupMemberCapability.All) != 0)
        {
            throw Error(MessagingErrorCodes.ManagedConversationInvalid, "تنظیمات گفتگو معتبر نیست.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.UpdateSettingsAsync(
            userId,
            conversationId,
            command.Visibility,
            command.GroupHistoryPolicy,
            command.MemberCapabilities,
            DecodeVersion(command.ExpectedVersion),
            context,
            UtcNow(),
            cancellationToken));
    }

    public async Task<DiscoverableConversationPageResult> DiscoverAsync(
        Guid userId,
        Guid sessionId,
        DiscoverConversationsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Type is not null and not ConversationType.Group and not ConversationType.Channel ||
            query.Page <= 0 || query.PageSize is <= 0 or > 100 || query.Query?.Length > 160)
        {
            throw Error(MessagingErrorCodes.MessagePageInvalid, "فیلتر یا صفحه‌بندی کشف گفتگو معتبر نیست.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var page = await store.DiscoverAsync(
            userId,
            query.Type,
            string.IsNullOrWhiteSpace(query.Query) ? null : query.Query.Trim(),
            query.Page,
            query.PageSize,
            context,
            cancellationToken);
        return new DiscoverableConversationPageResult(
            page.Items.Select(item => new DiscoverableConversationResult(
                item.Conversation.Id,
                item.Conversation.Type,
                item.Conversation.Scope,
                item.Conversation.Title ?? string.Empty,
                item.Conversation.Description,
                item.Conversation.OrganizationId.HasValue && item.OrganizationName is not null
                    ? new ConversationOrganizationBadgeResult(
                        item.Conversation.OrganizationId.Value,
                        item.OrganizationName)
                    : null,
                item.IsJoined,
                item.Conversation.CreatedAtUtc)).ToArray(),
            query.Page,
            query.PageSize,
            page.HasMore);
    }

    public async Task<ManagedConversationResult> JoinPublicAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        EnsureConversationId(conversationId);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.JoinPublicAsync(
            userId,
            conversationId,
            context,
            UtcNow(),
            cancellationToken));
    }

    public async Task<OwnershipTransferResult> StartOwnershipTransferAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        StartOwnershipTransferCommand command,
        CancellationToken cancellationToken)
    {
        EnsureConversationAndTarget(conversationId, command.TargetUserId);
        if (command.TargetUserId == userId)
        {
            throw Error(MessagingErrorCodes.OwnershipTransferConflict, "مالکیت باید به عضو دیگری منتقل شود.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.StartOwnershipTransferAsync(
            userId,
            conversationId,
            command.TargetUserId,
            context,
            UtcNow(),
            cancellationToken));
    }

    public async Task<OwnershipTransferResult> AcceptOwnershipTransferAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid transferId,
        CancellationToken cancellationToken) =>
        await CompleteTransferAsync(
            userId,
            sessionId,
            conversationId,
            transferId,
            true,
            cancellationToken);

    public async Task<OwnershipTransferResult> DeclineOwnershipTransferAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid transferId,
        CancellationToken cancellationToken) =>
        await CompleteTransferAsync(
            userId,
            sessionId,
            conversationId,
            transferId,
            false,
            cancellationToken);

    public async Task<ManagedConversationResult> ArchiveAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        EnsureConversationId(conversationId);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.ArchiveAsync(
            userId,
            conversationId,
            context,
            UtcNow(),
            cancellationToken));
    }

    private async Task<ManagedConversationResult> CreateAsync(
        Guid userId,
        Guid sessionId,
        ConversationType type,
        CreateManagedConversationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ClientOperationId == Guid.Empty || !Enum.IsDefined(command.Scope))
        {
            throw Error(MessagingErrorCodes.ManagedConversationInvalid, "شناسه عملیات و scope معتبر الزامی است.");
        }

        string title;
        string? description;
        try
        {
            title = Conversation.NormalizeTitle(command.Title);
            description = Conversation.NormalizeDescription(command.Description);
        }
        catch (ArgumentException exception)
        {
            throw new MessagingException(MessagingErrorCodes.ManagedConversationInvalid, exception.Message, exception);
        }

        var participantIds = (command.InitialParticipantUserIds ?? [])
            .OrderBy(id => id)
            .ToArray();
        if (participantIds.Any(id => id == Guid.Empty || id == userId) ||
            participantIds.Distinct().Count() != participantIds.Length)
        {
            throw Error(MessagingErrorCodes.ManagedConversationInvalid, "اعضای اولیه باید شناسه‌های یکتا و معتبر داشته باشند و شامل سازنده نباشند.");
        }

        var memberCapabilities = type == ConversationType.Group
            ? command.MemberCapabilities ?? GroupMemberCapability.LegacyMemberDefault
            : GroupMemberCapability.None;
        var historyPolicy = type == ConversationType.Group
            ? command.GroupHistoryPolicy
            : GroupHistoryPolicy.FullHistory;
        if (!Enum.IsDefined(command.Visibility) || !Enum.IsDefined(historyPolicy) ||
            (memberCapabilities & ~GroupMemberCapability.All) != 0)
        {
            throw Error(MessagingErrorCodes.ManagedConversationInvalid, "تنظیمات گفتگو معتبر نیست.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var hash = CreatePayloadHash(
            type,
            command.Scope,
            context.OrganizationId,
            title,
            description,
            participantIds,
            command.Visibility,
            historyPolicy,
            memberCapabilities);
        return Map(await store.CreateAsync(
            userId,
            type,
            command.Scope,
            title,
            description,
            participantIds,
            command.ClientOperationId,
            hash,
            command.Visibility,
            historyPolicy,
            memberCapabilities,
            context,
            UtcNow(),
            cancellationToken));
    }

    private async Task<OwnershipTransferResult> CompleteTransferAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid transferId,
        bool accept,
        CancellationToken cancellationToken)
    {
        EnsureConversationId(conversationId);
        if (transferId == Guid.Empty)
        {
            throw Error(MessagingErrorCodes.OwnershipTransferNotFound, "درخواست انتقال مالکیت یافت نشد.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var result = accept
            ? await store.AcceptOwnershipTransferAsync(userId, conversationId, transferId, context, UtcNow(), cancellationToken)
            : await store.DeclineOwnershipTransferAsync(userId, conversationId, transferId, context, UtcNow(), cancellationToken);
        return Map(result);
    }

    private static ManagedConversationResult Map(ManagedConversationStoreResult result) =>
        new(
            result.Conversation.Id,
            result.Conversation.Type,
            result.Conversation.Scope,
            result.Conversation.OrganizationId,
            result.Conversation.Title ?? string.Empty,
            result.Conversation.Description,
            result.Conversation.ManagementPolicy,
            result.Conversation.Visibility,
            result.Conversation.GroupHistoryPolicy,
            result.Conversation.MemberCapabilities,
            result.Conversation.Status,
            result.Conversation.CreatedAtUtc,
            result.Conversation.ArchivedAtUtc,
            Convert.ToBase64String(result.Conversation.RowVersion),
            result.Participants.Select(MapParticipant).ToArray(),
            result.AlreadyExisted);

    private static ConversationParticipantResult MapParticipant(
        ConversationParticipantStoreRecord item) =>
        new(
            item.Participant.Id,
            new SafeMessagingIdentityResult(
                item.Identity.UserId,
                item.Identity.Username,
                item.Identity.DisplayName),
            item.Participant.Role,
            item.Participant.Status,
            item.Participant.JoinedAtUtc,
            item.Participant.EndedAtUtc,
            item.Participant.HistoryVisibleFromSequence,
            item.Participant.AdminPermissions,
            Convert.ToBase64String(item.Participant.RowVersion));

    private static OwnershipTransferResult Map(OwnershipTransferStoreResult result) =>
        new(
            result.Transfer.Id,
            result.Transfer.ConversationId,
            result.Transfer.FromUserId,
            result.Transfer.ToUserId,
            result.Transfer.Status,
            result.Transfer.CreatedAtUtc,
            result.Transfer.AcceptedAtUtc,
            result.Transfer.DeclinedAtUtc,
            result.AlreadyCompleted);

    private static byte[] CreatePayloadHash(
        ConversationType type,
        ConversationScope scope,
        Guid? organizationId,
        string title,
        string? description,
        IReadOnlyCollection<Guid> participantIds,
        ConversationVisibility visibility,
        GroupHistoryPolicy groupHistoryPolicy,
        GroupMemberCapability memberCapabilities)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, ((int)type).ToString());
        Append(hash, ((int)scope).ToString());
        Append(hash, organizationId?.ToString("N") ?? string.Empty);
        Append(hash, title);
        Append(hash, description ?? string.Empty);
        Append(hash, ((int)visibility).ToString());
        Append(hash, ((int)groupHistoryPolicy).ToString());
        Append(hash, ((int)memberCapabilities).ToString());
        foreach (var participantId in participantIds)
        {
            Append(hash, participantId.ToString("N"));
        }

        return hash.GetHashAndReset();
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }

    private static void EnsureConversationAndTarget(Guid conversationId, Guid targetUserId)
    {
        EnsureConversationId(conversationId);
        if (targetUserId == Guid.Empty)
        {
            throw Error(MessagingErrorCodes.ParticipantNotFound, "عضو گفتگو یافت نشد.");
        }
    }

    private static void EnsureConversationId(Guid conversationId)
    {
        if (conversationId == Guid.Empty)
        {
            throw Error(MessagingErrorCodes.ConversationNotFound, "گفتگو یافت نشد.");
        }
    }

    private static byte[] DecodeVersion(string value)
    {
        try
        {
            var decoded = Convert.FromBase64String(value);
            if (decoded.Length != 8)
            {
                throw new FormatException();
            }

            return decoded;
        }
        catch (FormatException exception)
        {
            throw new MessagingException(
                MessagingErrorCodes.ConversationStateConflict,
                "نسخه مورد انتظار معتبر نیست.",
                exception);
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static MessagingException Error(string code, string message) => new(code, message);
}
