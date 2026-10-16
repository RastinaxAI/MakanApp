namespace MakanApp.Domain.Identity;

public enum CommunicationAgeVerificationStatus { Unverified = 0, Verified = 1, Conflict = 2 }
public enum CommunicationAgeVerificationSource { School = 1, PlatformResolution = 2, PlatformVerification = 3 }

public sealed class CommunicationAgeVerification
{
    private CommunicationAgeVerification() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly VerifiedBirthDate { get; private set; }
    public CommunicationAgeCategory PreviousCategory { get; private set; }
    public CommunicationAgeCategory ProposedCategory { get; private set; }
    public CommunicationAgeVerificationStatus Status { get; private set; }
    public CommunicationAgeVerificationSource Source { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string Reason { get; private set; } = null!;

    public static CommunicationAgeCategory Classify(DateOnly birthDate, DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(nowUtc);
        if (nowUtc.Kind != DateTimeKind.Utc || birthDate > today || birthDate < today.AddYears(-130))
            throw new ArgumentException("تاریخ تولد تأییدشده معتبر نیست.");
        return birthDate.AddYears(18) <= today ? CommunicationAgeCategory.Adult : CommunicationAgeCategory.Minor;
    }

    public static CommunicationAgeVerification Create(Guid userId, DateOnly birthDate,
        CommunicationAgeCategory previousCategory, bool conflict, CommunicationAgeVerificationSource source,
        Guid actorUserId, Guid? organizationId, string reason, DateTime nowUtc)
    {
        if (userId == Guid.Empty || actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500 ||
            !Enum.IsDefined(source) || (source == CommunicationAgeVerificationSource.School) != organizationId.HasValue)
            throw new ArgumentException("شاهد تأیید سن معتبر نیست.");
        return new CommunicationAgeVerification
        {
            Id = Guid.NewGuid(), UserId = userId, VerifiedBirthDate = birthDate,
            PreviousCategory = previousCategory, ProposedCategory = Classify(birthDate, nowUtc),
            Status = conflict ? CommunicationAgeVerificationStatus.Conflict : CommunicationAgeVerificationStatus.Verified,
            Source = source, ActorUserId = actorUserId, OrganizationId = organizationId,
            Reason = reason.Trim(), CreatedAtUtc = nowUtc
        };
    }
}
