using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed class ConversationAdvancedManagementService(IAccessContextResolver contexts, IConversationAdvancedManagementStore store,
    IMessagingStore messages, IContentSafetyProvider safety, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public async Task<CreatedInviteLinkResult> CreateLinkAsync(Guid userId, Guid sessionId, Guid conversationId, CreateInviteLinkCommand command, string correlationId, CancellationToken ct)
    {
        try { ConversationInviteLink.Create(command.ClientLinkId, conversationId, userId, new string('0', 64), command.ExpiresAtUtc, command.MaximumUses, command.RequiresApproval, Now); }
        catch (ArgumentException ex) { throw Invalid(ex); }
        return await store.CreateLinkAsync(userId, sessionId, conversationId, command, await contexts.ResolveAsync(userId, sessionId, ct), correlationId, Now, ct);
    }
    public async Task<InviteLinkPageResult> GetLinksAsync(Guid userId, Guid sessionId, Guid conversationId, int page, int pageSize, CancellationToken ct)
    {
        var skip = Page(page, pageSize);
        return await store.GetLinksAsync(userId, conversationId, await contexts.ResolveAsync(userId, sessionId, ct), skip, pageSize, ct);
    }
    public async Task<InviteLinkResult> RevokeLinkAsync(Guid userId, Guid sessionId, Guid conversationId, Guid linkId, RevokeInviteLinkCommand command, string correlationId, CancellationToken ct)
    {
        var version = Version(command.ExpectedVersion)!;
        return await store.RevokeLinkAsync(userId, sessionId, conversationId, linkId, version, await contexts.ResolveAsync(userId, sessionId, ct), correlationId, Now, ct);
    }
    public async Task<InviteLinkRedemptionResult> RedeemLinkAsync(Guid userId, Guid sessionId, RedeemInviteLinkCommand command, string correlationId, CancellationToken ct)
    {
        if (command.Token is null || command.Token.Length != 43 || command.Token.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new MessagingException(MessagingErrorCodes.InviteLinkUnavailable, "لینک در دسترس نیست.");
        return await store.RedeemLinkAsync(userId, sessionId, command.Token, await contexts.ResolveAsync(userId, sessionId, ct), correlationId, Now, ct);
    }
    public async Task<ConversationJoinRequestPageResult> GetJoinRequestsAsync(Guid userId, Guid sessionId, Guid? conversationId, int page, int pageSize, CancellationToken ct)
    {
        var skip = Page(page, pageSize);
        return await store.GetJoinRequestsAsync(userId, conversationId, await contexts.ResolveAsync(userId, sessionId, ct), skip, pageSize, ct);
    }
    public async Task<ConversationJoinRequestResult> CompleteJoinRequestAsync(Guid userId, Guid sessionId, Guid requestId,
        ConversationJoinRequestStatus status, CompleteJoinRequestCommand command, string correlationId, CancellationToken ct)
    {
        if (status is not ConversationJoinRequestStatus.Approved and not ConversationJoinRequestStatus.Rejected and not ConversationJoinRequestStatus.Cancelled) throw Invalid();
        var version = Version(command.ExpectedVersion)!;
        return await store.CompleteJoinRequestAsync(userId, sessionId, requestId, status, version, await contexts.ResolveAsync(userId, sessionId, ct), correlationId, Now, ct);
    }
    public async Task<MemberRestrictionPageResult> GetRestrictionsAsync(Guid userId, Guid sessionId, Guid conversationId, int page, int pageSize, CancellationToken ct)
    {
        var skip = Page(page, pageSize);
        return await store.GetRestrictionsAsync(userId, conversationId, await contexts.ResolveAsync(userId, sessionId, ct), skip, pageSize, Now, ct);
    }
    public async Task<MemberRestrictionResult> GetOwnRestrictionAsync(Guid userId, Guid sessionId, Guid conversationId, CancellationToken ct) =>
        await store.GetOwnRestrictionAsync(userId, conversationId, await contexts.ResolveAsync(userId, sessionId, ct), Now, ct);
    public async Task<MemberRestrictionResult> SetRestrictionAsync(Guid userId, Guid sessionId, Guid conversationId, Guid targetUserId,
        SetMemberRestrictionCommand command, string correlationId, CancellationToken ct)
    {
        try { ConversationMemberRestriction.Create(conversationId, targetUserId).Update(command.IsBanned, command.DeniedCapabilities, command.UntilUtc, Now); }
        catch (ArgumentException ex) { throw Invalid(ex); }
        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 400 || command.Reason.Any(char.IsControl)) throw Invalid();
        var version = Version(command.ExpectedVersion, true);
        return await store.SetRestrictionAsync(userId, sessionId, conversationId, targetUserId, command, version,
            await contexts.ResolveAsync(userId, sessionId, ct), correlationId, Now, ct);
    }
    public async Task<ConversationInfoResult> UpdateInfoAsync(Guid userId, Guid sessionId, Guid conversationId, UpdateConversationInfoCommand command, string correlationId, CancellationToken ct)
    {
        try { command = command with { Title = Conversation.NormalizeTitle(command.Title), Description = Conversation.NormalizeDescription(command.Description) }; }
        catch (ArgumentException ex) { throw Invalid(ex); }
        var version = Version(command.ExpectedVersion)!;
        var context = await contexts.ResolveAsync(userId, sessionId, ct);
        await store.PrepareInfoAsync(userId, conversationId, version, context, ct);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("conversation-info:" + JsonSerializer.Serialize(command)));
        var content = new SendMessageStoreCommand(new Guid(hash.AsSpan(0, 16)), MessageKind.Text,
            command.Title + "\n" + command.Description, [], null, [], SafetyPurpose: MessageSafetyPurpose.ConversationMetadata);
        var result = await MessageContentSafetyGate.ApplyAsync(messages, safety, clock, userId, conversationId, context,
            content, MessagingService.HashSendPayload(conversationId, content), ct);
        return await store.UpdateInfoAsync(userId, sessionId, conversationId, command, version, result.Evaluated,
            context, correlationId, Now, ct);
    }
    private static byte[]? Version(string? value, bool optional = false)
    {
        if (value is null && optional) return null;
        try { if (value is not null) { var bytes = Convert.FromBase64String(value); if (bytes.Length == 8) return bytes; } }
        catch (FormatException) { }
        throw Invalid();
    }
    private static int Page(int page, int size)
    {
        if (page is < 1 or > 100000 || size is < 1 or > 100) throw Invalid();
        return (page - 1) * size;
    }
    private static MessagingException Invalid(Exception? inner = null) => new(MessagingErrorCodes.AdvancedManagementInvalid, "درخواست مدیریت معتبر نیست.", inner);
}
