namespace MakanApp.Domain.Messaging;

public sealed class SafetyIncident
{
    public const int MaximumProviderReferenceLength = 200;
    public const int MaximumRiskLevelLength = 50;
    public const int MaximumPolicyCodesLength = 1000;

    private SafetyIncident()
    {
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid ClientMessageId { get; private set; }
    public MessageKind ContentKind { get; private set; }
    public ContentSafetyDecision Decision { get; private set; }
    public string? RiskLevel { get; private set; }
    public string? ProviderReference { get; private set; }
    public string? PolicyCodes { get; private set; }
    public string? ContentSnapshot { get; private set; }
    public byte[] ContentHash { get; private set; } = [];
    public byte[] RequestPayloadHash { get; private set; } = [];
    public SafetyIncidentStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static SafetyIncident Create(
        Guid conversationId,
        Guid actorUserId,
        Guid clientMessageId,
        MessageKind contentKind,
        ContentSafetyDecision decision,
        string? riskLevel,
        string? providerReference,
        string? policyCodes,
        string? contentSnapshot,
        byte[] contentHash,
        byte[] requestPayloadHash,
        DateTime createdAtUtc)
    {
        if (conversationId == Guid.Empty || actorUserId == Guid.Empty || clientMessageId == Guid.Empty ||
            !Enum.IsDefined(contentKind) ||
            decision is not ContentSafetyDecision.Blocked and not ContentSafetyDecision.ReviewRequired ||
            contentHash.Length != 32 || requestPayloadHash.Length != 32 ||
            createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("رخداد ایمنی محتوا معتبر نیست.");
        }

        return new SafetyIncident
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            ActorUserId = actorUserId,
            ClientMessageId = clientMessageId,
            ContentKind = contentKind,
            Decision = decision,
            RiskLevel = Normalize(riskLevel, MaximumRiskLevelLength, nameof(riskLevel)),
            ProviderReference = Normalize(providerReference, MaximumProviderReferenceLength, nameof(providerReference)),
            PolicyCodes = Normalize(policyCodes, MaximumPolicyCodesLength, nameof(policyCodes)),
            ContentSnapshot = Normalize(contentSnapshot, Message.StorageMaximumTextLength, nameof(contentSnapshot)),
            ContentHash = contentHash.ToArray(),
            RequestPayloadHash = requestPayloadHash.ToArray(),
            Status = SafetyIncidentStatus.Open,
            CreatedAtUtc = createdAtUtc
        };
    }

    public void Resolve(DateTime closedAtUtc)
    {
        Close(SafetyIncidentStatus.Resolved, closedAtUtc);
    }

    public void Dismiss(DateTime closedAtUtc)
    {
        Close(SafetyIncidentStatus.Dismissed, closedAtUtc);
    }

    private void Close(SafetyIncidentStatus status, DateTime closedAtUtc)
    {
        if (Status != SafetyIncidentStatus.Open || closedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("رخداد ایمنی در وضعیت قابل بستن نیست.");
        }

        Status = status;
        ClosedAtUtc = closedAtUtc;
    }

    private static string? Normalize(string? value, int maximumLength, string parameterName)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximumLength)
        {
            throw new ArgumentException("مقدار ایمنی محتوا از طول مجاز بیشتر است.", parameterName);
        }

        return normalized;
    }
}
