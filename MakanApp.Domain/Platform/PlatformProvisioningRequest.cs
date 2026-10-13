namespace MakanApp.Domain.Platform;

public enum PlatformProvisioningStatus
{
    Requested = 1,
    OrganizationCreated = 2,
    SubscriptionAssigned = 3,
    ManagerInvitationCreated = 4,
    Completed = 5,
    NeedsAttention = 6
}

public sealed class PlatformProvisioningRequest
{
    public const int MaximumClientOperationIdLength = 100;
    public const int MaximumOrganizationNameLength = 200;
    public const int MaximumReasonLength = 500;
    public const int MaximumFailureCodeLength = 100;

    private PlatformProvisioningRequest()
    {
    }

    public Guid Id { get; private set; }
    public string ClientOperationId { get; private set; } = string.Empty;
    public Guid RequestedByUserId { get; private set; }
    public string OrganizationName { get; private set; } = string.Empty;
    public Guid? PlanVersionId { get; private set; }
    public Guid? InitialManagerUserId { get; private set; }
    public DateTime? SubscriptionEndsAtUtc { get; private set; }
    public DateTime? InvitationExpiresAtUtc { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public PlatformProvisioningStatus Status { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? SubscriptionId { get; private set; }
    public Guid? ManagerInvitationId { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static PlatformProvisioningRequest Create(
        string clientOperationId,
        Guid requestedByUserId,
        string organizationName,
        Guid? planVersionId,
        Guid? initialManagerUserId,
        DateTime? subscriptionEndsAtUtc,
        DateTime? invitationExpiresAtUtc,
        string reason,
        DateTime createdAtUtc)
    {
        var operationId = clientOperationId?.Trim();
        var name = organizationName?.Trim();
        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > MaximumClientOperationIdLength ||
            requestedByUserId == Guid.Empty || string.IsNullOrWhiteSpace(name) ||
            name.Length > MaximumOrganizationNameLength ||
            string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > MaximumReasonLength ||
            planVersionId == Guid.Empty || initialManagerUserId == Guid.Empty)
        {
            throw new ArgumentException("درخواست provision معتبر نیست.");
        }

        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        EnsureOptionalFutureUtc(subscriptionEndsAtUtc, createdAtUtc, nameof(subscriptionEndsAtUtc));
        EnsureOptionalFutureUtc(invitationExpiresAtUtc, createdAtUtc, nameof(invitationExpiresAtUtc));
        if (initialManagerUserId.HasValue && !invitationExpiresAtUtc.HasValue)
        {
            throw new ArgumentException("زمان انقضای دعوت مدیر اولیه الزامی است.", nameof(invitationExpiresAtUtc));
        }

        return new PlatformProvisioningRequest
        {
            Id = Guid.NewGuid(),
            ClientOperationId = operationId,
            RequestedByUserId = requestedByUserId,
            OrganizationName = name,
            PlanVersionId = planVersionId,
            InitialManagerUserId = initialManagerUserId,
            SubscriptionEndsAtUtc = subscriptionEndsAtUtc,
            InvitationExpiresAtUtc = invitationExpiresAtUtc,
            Reason = normalizedReason,
            Status = PlatformProvisioningStatus.Requested,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
    }

    public bool Matches(
        string organizationName,
        Guid? planVersionId,
        Guid? initialManagerUserId,
        DateTime? subscriptionEndsAtUtc,
        DateTime? invitationExpiresAtUtc) =>
        string.Equals(OrganizationName, organizationName.Trim(), StringComparison.Ordinal) &&
        PlanVersionId == planVersionId && InitialManagerUserId == initialManagerUserId &&
        SubscriptionEndsAtUtc == subscriptionEndsAtUtc && InvitationExpiresAtUtc == invitationExpiresAtUtc;

    public void MarkOrganizationCreated(Guid organizationId, DateTime updatedAtUtc)
    {
        EnsureIdentifier(organizationId, nameof(organizationId));
        OrganizationId ??= organizationId;
        Advance(PlatformProvisioningStatus.OrganizationCreated, updatedAtUtc);
    }

    public void MarkSubscriptionAssigned(Guid subscriptionId, DateTime updatedAtUtc)
    {
        EnsureIdentifier(subscriptionId, nameof(subscriptionId));
        SubscriptionId ??= subscriptionId;
        Advance(PlatformProvisioningStatus.SubscriptionAssigned, updatedAtUtc);
    }

    public void MarkManagerInvitationCreated(Guid invitationId, DateTime updatedAtUtc)
    {
        EnsureIdentifier(invitationId, nameof(invitationId));
        ManagerInvitationId ??= invitationId;
        Advance(PlatformProvisioningStatus.ManagerInvitationCreated, updatedAtUtc);
    }

    public void Complete(DateTime updatedAtUtc)
    {
        if (!OrganizationId.HasValue || PlanVersionId.HasValue && !SubscriptionId.HasValue ||
            InitialManagerUserId.HasValue && !ManagerInvitationId.HasValue)
        {
            throw new InvalidOperationException("همه مراحل الزامی provisioning تکمیل نشده‌اند.");
        }

        Advance(PlatformProvisioningStatus.Completed, updatedAtUtc);
    }

    public void MarkNeedsAttention(string failureCode, DateTime updatedAtUtc)
    {
        var normalized = failureCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > MaximumFailureCodeLength)
        {
            throw new ArgumentException("کد خطای provisioning معتبر نیست.", nameof(failureCode));
        }

        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
        FailureCode = normalized;
        Status = PlatformProvisioningStatus.NeedsAttention;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Resume(DateTime updatedAtUtc)
    {
        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
        if (Status != PlatformProvisioningStatus.NeedsAttention)
        {
            return;
        }

        FailureCode = null;
        Status = ManagerInvitationId.HasValue
            ? PlatformProvisioningStatus.ManagerInvitationCreated
            : SubscriptionId.HasValue
                ? PlatformProvisioningStatus.SubscriptionAssigned
                : OrganizationId.HasValue
                    ? PlatformProvisioningStatus.OrganizationCreated
                    : PlatformProvisioningStatus.Requested;
        UpdatedAtUtc = updatedAtUtc;
    }

    private void Advance(PlatformProvisioningStatus status, DateTime updatedAtUtc)
    {
        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
        FailureCode = null;
        Status = status;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static void EnsureIdentifier(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("شناسه provisioning معتبر نیست.", parameterName);
        }
    }

    private static void EnsureOptionalFutureUtc(DateTime? value, DateTime minimum, string parameterName)
    {
        if (!value.HasValue)
        {
            return;
        }

        EnsureUtc(value.Value, parameterName);
        if (value <= minimum)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان provisioning باید UTC باشد.", parameterName);
        }
    }
}
