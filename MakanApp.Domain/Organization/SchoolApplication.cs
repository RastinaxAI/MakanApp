namespace MakanApp.Domain.Organization;

public enum SchoolApplicationKind { LegalOrganization = 1, IndividualTeacher = 2 }
public enum SchoolApplicationStatus { Draft = 1, Submitted = 2, UnderReview = 3, NeedsChanges = 4, Rejected = 5, Approved = 6, Cancelled = 7 }
public sealed record SchoolApplicationDraftData(string? SchoolName, string? LegalEntityName, string? TeachingHistory,
    string? WorkSamples, string? DegreeField, int? StudentCapacity, int? TeacherCapacity);

public sealed class SchoolApplication
{
    private SchoolApplication() { }
    public Guid Id { get; private set; }
    public Guid ApplicantUserId { get; private set; }
    public Guid ProposedInitialManagerUserId => ApplicantUserId;
    public Guid? FinalInitialManagerUserId { get; private set; }
    public Guid? InitialManagerConfirmedByUserId { get; private set; }
    public DateTime? InitialManagerConfirmedAtUtc { get; private set; }
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
        if (data.StudentCapacity is < 0 || data.TeacherCapacity is < 0) throw new ArgumentException("ظرفیت منفی مجاز نیست.");
        var schoolName = Text(data.SchoolName, 200); var legalName = Text(data.LegalEntityName, 200);
        var teachingHistory = Text(data.TeachingHistory, 4000); var workSamples = Text(data.WorkSamples, 4000); var degreeField = Text(data.DegreeField, 500);
        SchoolName = schoolName; LegalEntityName = legalName; TeachingHistory = teachingHistory; WorkSamples = workSamples; DegreeField = degreeField;
        StudentCapacity = data.StudentCapacity; TeacherCapacity = data.TeacherCapacity;
        Revision++; Version++; UpdatedAtUtc = now;
    }
    public SchoolApplicationDraftData DraftData() => new(SchoolName, LegalEntityName, TeachingHistory, WorkSamples, DegreeField, StudentCapacity, TeacherCapacity);
    public void Submit(string approvedPolicyVersion, DateTime now, IEnumerable<string> documentKinds)
    {
        if (Status is not (SchoolApplicationStatus.Draft or SchoolApplicationStatus.NeedsChanges) || string.IsNullOrWhiteSpace(approvedPolicyVersion)) throw new InvalidOperationException("ارسال درخواست مجاز نیست.");
        if (SubmissionRequirements(documentKinds).Count != 0) throw new ArgumentException("اطلاعات و مدارک ارسال کامل نیست.");
        EnsureUtc(now); SubmittedPolicyVersion = approvedPolicyVersion; Status = SchoolApplicationStatus.Submitted; Version++; UpdatedAtUtc = now;
    }
    public IReadOnlyList<string> SubmissionRequirements(IEnumerable<string> documentKinds)
    {
        var missing = new List<string>();
        if (SchoolName is null) missing.Add(nameof(SchoolName));
        if (!StudentCapacity.HasValue) missing.Add(nameof(StudentCapacity));
        if (!TeacherCapacity.HasValue) missing.Add(nameof(TeacherCapacity));
        var kinds = documentKinds.ToHashSet(StringComparer.Ordinal);
        if (Kind == SchoolApplicationKind.LegalOrganization)
        {
            if (LegalEntityName is null) missing.Add(nameof(LegalEntityName));
            if (!kinds.Overlaps(["License", "OrganizationDocument"])) missing.Add("LegalDocument");
        }
        else
        {
            if (TeachingHistory is null) missing.Add(nameof(TeachingHistory));
            if (WorkSamples is null) missing.Add(nameof(WorkSamples));
            if (!kinds.Contains("Degree")) missing.Add("DegreeDocument");
        }
        return missing;
    }
    public void Cancel(DateTime now)
    {
        if (Status is not (SchoolApplicationStatus.Draft or SchoolApplicationStatus.Submitted or SchoolApplicationStatus.UnderReview or SchoolApplicationStatus.NeedsChanges))
            throw new InvalidOperationException("درخواست نهایی قابل لغو نیست.");
        EnsureUtc(now); Status = SchoolApplicationStatus.Cancelled; Version++; UpdatedAtUtc = now;
    }
    public bool CanCancel => Status is SchoolApplicationStatus.Draft or SchoolApplicationStatus.Submitted or SchoolApplicationStatus.UnderReview or SchoolApplicationStatus.NeedsChanges;
    public bool CanEdit => Status is SchoolApplicationStatus.Draft or SchoolApplicationStatus.NeedsChanges;
    public void StartReview(DateTime now)
    {
        if (Status != SchoolApplicationStatus.Submitted) throw new InvalidOperationException("درخواست قابل شروع بررسی نیست.");
        EnsureUtc(now); Status = SchoolApplicationStatus.UnderReview; Version++; UpdatedAtUtc = now;
    }
    public void Decide(SchoolApplicationStatus decision, DateTime now, Guid? initialManagerUserId = null, Guid? reviewerUserId = null)
    {
        if (Status != SchoolApplicationStatus.UnderReview || decision is not (SchoolApplicationStatus.NeedsChanges or SchoolApplicationStatus.Rejected or SchoolApplicationStatus.Approved))
            throw new InvalidOperationException("تصمیم با وضعیت درخواست سازگار نیست.");
        EnsureUtc(now);
        if (decision == SchoolApplicationStatus.Approved) SetInitialManager(initialManagerUserId, reviewerUserId, now);
        else if (initialManagerUserId.HasValue) throw new ArgumentException("انتخاب مدیر فقط در تأیید نهایی مجاز است.");
        Status = decision; Version++; UpdatedAtUtc = now;
    }
    public void ConfirmMissingInitialManager(Guid managerUserId, Guid reviewerUserId, DateTime now)
    {
        if (Status != SchoolApplicationStatus.Approved || FinalInitialManagerUserId.HasValue)
            throw new InvalidOperationException("فقط انتخاب ثبت‌نشدهٔ پروندهٔ تأییدشده قابل تکمیل است؛ جایگزینی مدیر مجاز نیست.");
        EnsureUtc(now); SetInitialManager(managerUserId, reviewerUserId, now); Version++; UpdatedAtUtc = now;
    }
    public void RecordProvisioningRetry(DateTime now)
    {
        if (Status != SchoolApplicationStatus.Approved || !FinalInitialManagerUserId.HasValue)
            throw new InvalidOperationException("بازیابی فقط برای درخواست تأییدشده با مدیر نهایی مجاز است.");
        EnsureUtc(now); Version++; UpdatedAtUtc = now;
    }
    private void SetInitialManager(Guid? manager, Guid? reviewer, DateTime now)
    {
        if (!manager.HasValue || manager == Guid.Empty || !reviewer.HasValue || reviewer == Guid.Empty)
            throw new ArgumentException("تأیید صریح مدیر اولیه و بازبین الزامی است.");
        FinalInitialManagerUserId = manager; InitialManagerConfirmedByUserId = reviewer; InitialManagerConfirmedAtUtc = now;
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
    public long? ApplicationVersion { get; private set; }
    public Guid? ProposedInitialManagerUserId { get; private set; }
    public Guid? FinalInitialManagerUserId { get; private set; }
    public Guid ReviewerUserId { get; private set; }
    public SchoolApplicationStatus Status { get; private set; }
    public string ApplicantFeedback { get; private set; } = "";
    public DateTime OccurredAtUtc { get; private set; }
    public static SchoolApplicationDecision Create(SchoolApplication app, Guid reviewer, string feedback, DateTime now) => new()
    { Id = Guid.NewGuid(), ApplicationId = app.Id, Revision = app.Revision, ApplicationVersion = app.Version,
        ProposedInitialManagerUserId = app.ProposedInitialManagerUserId, FinalInitialManagerUserId = app.FinalInitialManagerUserId,
        ReviewerUserId = reviewer, Status = app.Status, ApplicantFeedback = feedback, OccurredAtUtc = now };
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
