namespace MakanApp.Domain.Organization;

public enum SchoolApplicationKind { LegalOrganization = 1, IndividualTeacher = 2 }
public enum SchoolApplicationStatus { Draft = 1, Submitted = 2, UnderReview = 3, NeedsChanges = 4, Rejected = 5, Approved = 6 }
public sealed record SchoolApplicationDraftData(string? SchoolName, string? LegalEntityName, string? TeachingHistory,
    string? WorkSamples, string? DegreeField, int? StudentCapacity, int? TeacherCapacity);

public sealed class SchoolApplication
{
    private SchoolApplication() { }
    public Guid Id { get; private set; }
    public Guid ApplicantUserId { get; private set; }
    public SchoolApplicationKind Kind { get; private set; }
    public SchoolApplicationStatus Status { get; private set; }
    public int Revision { get; private set; }
    public long Version { get; private set; }
    public string? SchoolName { get; private set; }
    public string? LegalEntityName { get; private set; }
    public string? TeachingHistory { get; private set; }
    public string? WorkSamples { get; private set; }
    public string? DegreeField { get; private set; }
    public int? StudentCapacity { get; private set; }
    public int? TeacherCapacity { get; private set; }
    public string? SubmittedPolicyVersion { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static SchoolApplication Create(Guid applicant, SchoolApplicationKind kind, SchoolApplicationDraftData data, DateTime now)
    {
        if (applicant == Guid.Empty || !Enum.IsDefined(kind)) throw new ArgumentException("نوع درخواست معتبر نیست.");
        var request = new SchoolApplication { Id = Guid.NewGuid(), ApplicantUserId = applicant, Kind = kind, Status = SchoolApplicationStatus.Draft, CreatedAtUtc = now };
        request.UpdateDraft(data, now); return request;
    }
    public void UpdateDraft(SchoolApplicationDraftData data, DateTime now)
    {
        if (Status is not (SchoolApplicationStatus.Draft or SchoolApplicationStatus.NeedsChanges)) throw new InvalidOperationException("این وضعیت قابل اصلاح نیست.");
        EnsureUtc(now);
        SchoolName = Text(data.SchoolName, 200); LegalEntityName = Text(data.LegalEntityName, 200);
        TeachingHistory = Text(data.TeachingHistory, 4000); WorkSamples = Text(data.WorkSamples, 4000); DegreeField = Text(data.DegreeField, 500);
        // Draft مقدار صریح فرم را نگه می‌دارد؛ requiredness و دامنهٔ ظرفیت Submit هنوز تصمیم مصوب ندارد.
        StudentCapacity = data.StudentCapacity; TeacherCapacity = data.TeacherCapacity;
        Revision++; Version++; UpdatedAtUtc = now;
    }
    public SchoolApplicationDraftData DraftData() => new(SchoolName, LegalEntityName, TeachingHistory, WorkSamples, DegreeField, StudentCapacity, TeacherCapacity);
    public void Submit(string approvedPolicyVersion, DateTime now)
    {
        if (Status is not (SchoolApplicationStatus.Draft or SchoolApplicationStatus.NeedsChanges) || string.IsNullOrWhiteSpace(approvedPolicyVersion)) throw new InvalidOperationException("ارسال درخواست مجاز نیست.");
        EnsureUtc(now); SubmittedPolicyVersion = approvedPolicyVersion; Status = SchoolApplicationStatus.Submitted; Version++; UpdatedAtUtc = now;
    }
    public void StartReview(DateTime now)
    {
        if (Status != SchoolApplicationStatus.Submitted) throw new InvalidOperationException("درخواست قابل شروع بررسی نیست.");
        EnsureUtc(now); Status = SchoolApplicationStatus.UnderReview; Version++; UpdatedAtUtc = now;
    }
    public void Decide(SchoolApplicationStatus decision, DateTime now)
    {
        if (Status != SchoolApplicationStatus.UnderReview || decision is not (SchoolApplicationStatus.NeedsChanges or SchoolApplicationStatus.Rejected or SchoolApplicationStatus.Approved))
            throw new InvalidOperationException("تصمیم با وضعیت درخواست سازگار نیست.");
        EnsureUtc(now); Status = decision; Version++; UpdatedAtUtc = now;
    }
    private static void EnsureUtc(DateTime now) { if (now.Kind != DateTimeKind.Utc) throw new ArgumentException("زمان باید UTC باشد."); }
    private static string? Text(string? value, int max)
    {
        var text = value?.Trim();
        if (text?.Length > max || text?.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t') == true) throw new ArgumentException("متن درخواست معتبر نیست.");
        return string.IsNullOrEmpty(text) ? null : text;
    }
}

public sealed class SchoolApplicationRevision
{
    private SchoolApplicationRevision() { }
    public Guid ApplicationId { get; private set; }
    public int Number { get; private set; }
    public string ProtectedSnapshot { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public static SchoolApplicationRevision Create(Guid application, int revision, string snapshot, DateTime now) =>
        new() { ApplicationId = application, Number = revision, ProtectedSnapshot = snapshot, CreatedAtUtc = now };
}
public sealed class SchoolApplicationDocument
{
    private SchoolApplicationDocument() { }
    public Guid ApplicationId { get; private set; }
    public int Revision { get; private set; }
    public Guid FileAssetId { get; private set; }
    public string Kind { get; private set; } = "";
    public static SchoolApplicationDocument Create(Guid application, int revision, Guid file, string kind) =>
        new() { ApplicationId = application, Revision = revision, FileAssetId = file, Kind = kind };
}
public sealed class SchoolApplicationDecision
{
    private SchoolApplicationDecision() { }
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public int Revision { get; private set; }
    public Guid ReviewerUserId { get; private set; }
    public SchoolApplicationStatus Status { get; private set; }
    public string ApplicantFeedback { get; private set; } = "";
    public DateTime OccurredAtUtc { get; private set; }
    public static SchoolApplicationDecision Create(SchoolApplication app, Guid reviewer, string feedback, DateTime now) => new()
    { Id = Guid.NewGuid(), ApplicationId = app.Id, Revision = app.Revision, ReviewerUserId = reviewer, Status = app.Status, ApplicantFeedback = feedback, OccurredAtUtc = now };
}
public sealed class SchoolApplicationOperation
{
    private SchoolApplicationOperation() { }
    public Guid ActorUserId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public DateTime OccurredAtUtc { get; private set; }
    public static SchoolApplicationOperation Create(Guid actor, Guid operation, Guid application, string hash, DateTime now) =>
        new() { ActorUserId = actor, ClientOperationId = operation, ApplicationId = application, Fingerprint = hash, OccurredAtUtc = now };
}
