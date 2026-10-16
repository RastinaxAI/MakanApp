using MakanApp.Domain.Organization;

namespace MakanApp.Domain.Messaging;

public enum ScheduledMessageStatus { Scheduled = 1, Dispatched = 2, Cancelled = 3, Failed = 4 }
public sealed record ScheduledMessageAuthority(WorkspaceType WorkspaceType, Guid? OrganizationId,
    Guid? MembershipId, OrganizationRole? Role, Guid? SubjectOrganizationPersonId);
public sealed record ScheduledMessageContent(MessageKind Kind, string? Text, IReadOnlyCollection<Guid> AttachmentIds,
    Guid? ReplyToMessageId, IReadOnlyCollection<Guid> MentionedUserIds, Guid? ContactUserId, Guid? TopicId, bool IsSilent);

public sealed class ScheduledMessage
{
    public const int MaximumAttempts = 5;
    public const int MaximumPendingPerConversationSender = 100;
    private readonly List<ScheduledMessageAttachment> _attachments = [];
    private readonly List<ScheduledMessageMention> _mentions = [];
    private ScheduledMessage() { }
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ClientMessageId { get; private set; }
    public WorkspaceType WorkspaceType { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? MembershipId { get; private set; }
    public OrganizationRole? Role { get; private set; }
    public Guid? SubjectOrganizationPersonId { get; private set; }
    public MessageKind Kind { get; private set; }
    public string? Text { get; private set; }
    public Guid? ReplyToMessageId { get; private set; }
    public Guid? ContactUserId { get; private set; }
    public Guid? TopicId { get; private set; }
    public bool IsSilent { get; private set; }
    public byte[] PayloadHash { get; private set; } = [];
    public IReadOnlyCollection<ScheduledMessageAttachment> Attachments => _attachments.AsReadOnly();
    public IReadOnlyCollection<ScheduledMessageMention> Mentions => _mentions.AsReadOnly();
    public ScheduledMessageStatus Status { get; private set; }
    public DateTime ScheduledAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public Guid? DispatchedMessageId { get; private set; }
    public Guid? ClaimToken { get; private set; }
    public DateTime? ClaimedUntilUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public string? LastFailureCode { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ScheduledMessage Create(Guid conversationId, Guid userId, Guid clientMessageId, ScheduledMessageAuthority authority,
        ScheduledMessageContent content, byte[] payloadHash, DateTime scheduledAtUtc, DateTime nowUtc)
    {
        Utc(nowUtc); Utc(scheduledAtUtc);
        if (conversationId == Guid.Empty || userId == Guid.Empty || clientMessageId == Guid.Empty || scheduledAtUtc <= nowUtc ||
            payloadHash.Length != 32 || content.TopicId == Guid.Empty || content.ReplyToMessageId == Guid.Empty ||
            content.AttachmentIds.Any(id => id == Guid.Empty) || content.MentionedUserIds.Any(id => id == Guid.Empty) ||
            !Enum.IsDefined(authority.WorkspaceType) ||
            authority.WorkspaceType == WorkspaceType.Personal && (authority.OrganizationId is not null || authority.MembershipId is not null || authority.Role is not null || authority.SubjectOrganizationPersonId is not null) ||
            authority.WorkspaceType == WorkspaceType.Organization && (authority.OrganizationId is null || authority.OrganizationId == Guid.Empty ||
                authority.MembershipId is null || authority.MembershipId == Guid.Empty || authority.Role is null || !Enum.IsDefined(authority.Role.Value)))
            throw new ArgumentException("محتوا، زمان یا اختیار ثبت‌شده برای زمان‌بندی معتبر نیست.");
        Message.ValidateContent(content.Kind, content.Text, content.AttachmentIds.Count, Message.StorageMaximumTextLength);
        if (content.Kind == MessageKind.ContactCard && (content.ContactUserId is null || content.ContactUserId == Guid.Empty) ||
            content.Kind != MessageKind.ContactCard && content.ContactUserId is not null)
            throw new ArgumentException("کارت مخاطب معتبر نیست.");
        var result = new ScheduledMessage { Id = Guid.NewGuid(), ConversationId = conversationId, UserId = userId, ClientMessageId = clientMessageId,
            WorkspaceType = authority.WorkspaceType, OrganizationId = authority.OrganizationId, MembershipId = authority.MembershipId, Role = authority.Role,
            SubjectOrganizationPersonId = authority.SubjectOrganizationPersonId, Kind = content.Kind, Text = content.Text, ReplyToMessageId = content.ReplyToMessageId,
            ContactUserId = content.ContactUserId, TopicId = content.TopicId, IsSilent = content.IsSilent, PayloadHash = payloadHash.ToArray(),
            ScheduledAtUtc = scheduledAtUtc, CreatedAtUtc = nowUtc, Status = ScheduledMessageStatus.Scheduled };
        result._attachments.AddRange(content.AttachmentIds.Distinct().Select(id => new ScheduledMessageAttachment(result.Id, id)));
        result._mentions.AddRange(content.MentionedUserIds.Distinct().Select(id => new ScheduledMessageMention(result.Id, id)));
        return result;
    }
    public bool HasClaim(Guid token, DateTime nowUtc) => Status == ScheduledMessageStatus.Scheduled && ClaimToken == token && ClaimedUntilUtc > nowUtc;
    public void Claim(Guid token, DateTime nowUtc, DateTime untilUtc)
    {
        Utc(nowUtc); Utc(untilUtc);
        if (token == Guid.Empty || untilUtc <= nowUtc || Status != ScheduledMessageStatus.Scheduled || ScheduledAtUtc > nowUtc ||
            ClaimedUntilUtc > nowUtc || NextAttemptAtUtc > nowUtc || AttemptCount >= MaximumAttempts) throw new InvalidOperationException("کار قابل تصاحب نیست.");
        ClaimToken = token; ClaimedUntilUtc = untilUtc; AttemptCount++; NextAttemptAtUtc = null;
    }
    public void Dispatch(Guid token, Guid messageId, DateTime nowUtc)
    {
        Utc(nowUtc);
        if (!HasClaim(token, nowUtc) || messageId == Guid.Empty) throw new InvalidOperationException("تصاحب معتبر نیست.");
        LastFailureCode = null; DispatchedMessageId = messageId; Complete(ScheduledMessageStatus.Dispatched, nowUtc);
    }
    public void Cancel(DateTime nowUtc)
    {
        Utc(nowUtc);
        if (Status == ScheduledMessageStatus.Cancelled) return;
        if (Status != ScheduledMessageStatus.Scheduled) throw new InvalidOperationException("کار نهایی‌شده قابل لغو نیست.");
        Complete(ScheduledMessageStatus.Cancelled, nowUtc);
    }
    public void Fail(Guid token, string code, bool retryable, DateTime nowUtc)
    {
        Utc(nowUtc);
        if (!HasClaim(token, nowUtc)) throw new InvalidOperationException("تصاحب معتبر نیست.");
        Code(code); LastFailureCode = code;
        if (!retryable || AttemptCount >= MaximumAttempts) Complete(ScheduledMessageStatus.Failed, nowUtc);
        else { ClaimToken = null; ClaimedUntilUtc = null; NextAttemptAtUtc = nowUtc.AddSeconds(5 * (1 << (AttemptCount - 1))); }
    }
    public void FailExhausted(DateTime nowUtc)
    {
        Utc(nowUtc);
        if (Status != ScheduledMessageStatus.Scheduled || AttemptCount < MaximumAttempts || ClaimedUntilUtc > nowUtc)
            throw new InvalidOperationException("کار هنوز قابل بازیابی است.");
        LastFailureCode = "SCHEDULED_MAXIMUM_ATTEMPTS"; Complete(ScheduledMessageStatus.Failed, nowUtc);
    }
    private void Complete(ScheduledMessageStatus status, DateTime nowUtc)
    {
        Status = status; CompletedAtUtc = nowUtc; ClaimToken = null; ClaimedUntilUtc = null; NextAttemptAtUtc = null;
    }
    private static void Utc(DateTime value) { if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("زمان باید UTC باشد."); }
    private static void Code(string code) { if (string.IsNullOrWhiteSpace(code) || code.Length > 100 || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_')) throw new ArgumentException("کد خطا معتبر نیست."); }
}
public sealed class ScheduledMessageAttachment
{
    private ScheduledMessageAttachment() { }
    internal ScheduledMessageAttachment(Guid scheduledMessageId, Guid fileAssetId) { ScheduledMessageId = scheduledMessageId; FileAssetId = fileAssetId; }
    public Guid ScheduledMessageId { get; private set; }
    public Guid FileAssetId { get; private set; }
}
public sealed class ScheduledMessageMention
{
    private ScheduledMessageMention() { }
    internal ScheduledMessageMention(Guid scheduledMessageId, Guid userId) { ScheduledMessageId = scheduledMessageId; UserId = userId; }
    public Guid ScheduledMessageId { get; private set; }
    public Guid UserId { get; private set; }
}
