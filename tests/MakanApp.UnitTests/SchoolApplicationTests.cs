using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests;

public sealed class SchoolApplicationTests
{
    [Theory]
    [InlineData(SchoolApplicationKind.LegalOrganization)]
    [InlineData(SchoolApplicationKind.IndividualTeacher)]
    public void DraftDoesNotInventCapacityAndApprovalIsSeparateFromProvisioning(SchoolApplicationKind kind)
    {
        var now = DateTime.UtcNow;
        var app = SchoolApplication.Create(Guid.NewGuid(), kind, new(null, null, null, null, null, null, null), now);
        Assert.Null(app.StudentCapacity); Assert.Null(app.TeacherCapacity); Assert.Equal(SchoolApplicationStatus.Draft, app.Status);
        Assert.Throws<InvalidOperationException>(() => app.StartReview(now));
        Assert.Throws<ArgumentException>(() => app.Submit("approved-policy", now, []));
        app.UpdateDraft(new("نام صریح", "نام حقوقی", "سابقه", "نمونه", null, 37, 0), now);
        string[] documents = kind == SchoolApplicationKind.LegalOrganization ? ["License"] : ["Degree"];
        app.Submit("approved-policy", now, documents); app.StartReview(now); app.Decide(SchoolApplicationStatus.NeedsChanges, now);
        app.UpdateDraft(app.DraftData() with { StudentCapacity = 38 }, now);
        Assert.Equal(38, app.StudentCapacity); Assert.Equal(0, app.TeacherCapacity); Assert.Equal(3, app.Revision);
        app.Submit("approved-policy", now, documents); app.StartReview(now); app.Decide(SchoolApplicationStatus.Approved, now, app.ApplicantUserId, Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => app.UpdateDraft(new(null, null, null, null, null, null, null), now));
        Assert.Throws<InvalidOperationException>(() => app.Decide(SchoolApplicationStatus.NeedsChanges, now));
        Assert.Throws<InvalidOperationException>(() => app.Cancel(now));
    }

    [Theory]
    [InlineData(0)] [InlineData(999)]
    public void UnknownApplicationKindCannotCreateARequest(int kind) =>
        Assert.Throws<ArgumentException>(() => SchoolApplication.Create(Guid.NewGuid(), (SchoolApplicationKind)kind,
            new(null, null, null, null, null, null, null), DateTime.UtcNow));

    [Theory]
    [InlineData(-1, 0)] [InlineData(0, -1)] [InlineData(-1, -1)]
    public void NegativeCapacitiesAreRejectedWithoutInventingPositiveMinimum(int learners, int teachers) =>
        Assert.Throws<ArgumentException>(() => SchoolApplication.Create(Guid.NewGuid(), SchoolApplicationKind.LegalOrganization,
            new("مدرسه", "مؤسسه", null, null, null, learners, teachers), DateTime.UtcNow));

    [Theory]
    [InlineData(SchoolApplicationKind.LegalOrganization, "License")]
    [InlineData(SchoolApplicationKind.LegalOrganization, "OrganizationDocument")]
    [InlineData(SchoolApplicationKind.IndividualTeacher, "Degree")]
    public void ExplicitZeroCapacitiesAndApprovedDocumentCategoryCanSubmit(SchoolApplicationKind kind, string document)
    {
        var app = SchoolApplication.Create(Guid.NewGuid(), kind, new("مدرسه", "مؤسسه", "سابقه", "نمونه", null, 0, 0), DateTime.UtcNow);
        Assert.Empty(app.SubmissionRequirements([document]));
        app.Submit("approved", DateTime.UtcNow, [document]);
        Assert.Equal(SchoolApplicationStatus.Submitted, app.Status);
    }

    [Theory]
    [InlineData(SchoolApplicationKind.LegalOrganization)] [InlineData(SchoolApplicationKind.IndividualTeacher)]
    public void RequiredFieldsAndTypedDocumentAreNotInferredFromUnrelatedFile(SchoolApplicationKind kind)
    {
        var app = SchoolApplication.Create(Guid.NewGuid(), kind, new(null, null, null, null, null, null, null), DateTime.UtcNow);
        var missing = app.SubmissionRequirements(["OtherSupportingDocument"]);
        Assert.Contains("SchoolName", missing); Assert.Contains("StudentCapacity", missing); Assert.Contains("TeacherCapacity", missing);
        Assert.Contains(kind == SchoolApplicationKind.LegalOrganization ? "LegalDocument" : "DegreeDocument", missing);
        Assert.Throws<ArgumentException>(() => app.Submit("approved", DateTime.UtcNow, ["OtherSupportingDocument"]));
    }

    [Fact]
    public void CancellationIsFinalAndDoesNotCreateSubmissionPolicy()
    {
        var app = SchoolApplication.Create(Guid.NewGuid(), SchoolApplicationKind.LegalOrganization, new(null, null, null, null, null, null, null), DateTime.UtcNow);
        app.Cancel(DateTime.UtcNow); Assert.Equal(SchoolApplicationStatus.Cancelled, app.Status); Assert.Null(app.SubmittedPolicyVersion);
        Assert.False(app.CanEdit); Assert.False(app.CanCancel);
        Assert.Throws<InvalidOperationException>(() => app.Submit("approved", DateTime.UtcNow, []));
        Assert.Throws<InvalidOperationException>(() => app.UpdateDraft(app.DraftData(), DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => app.Cancel(DateTime.UtcNow));
    }

    [Fact]
    public void InvalidDraftUpdateDoesNotPartiallyMutateTheAggregate()
    {
        var app = SchoolApplication.Create(Guid.NewGuid(), SchoolApplicationKind.LegalOrganization, new("نام قبلی", null, null, null, null, null, null), DateTime.UtcNow);
        var original = app.DraftData(); var version = app.Version;
        Assert.Throws<ArgumentException>(() => app.UpdateDraft(original with { SchoolName = "نام تازه", WorkSamples = new string('x', 4001) }, DateTime.UtcNow));
        Assert.Equal(original, app.DraftData()); Assert.Equal(version, app.Version);
    }

    [Fact]
    public void PaidSchoolIntentRequiresManagerPolicyAndCannotBeResumedAsGenericProvisioning()
    {
        var app = Guid.NewGuid(); var order = Guid.NewGuid();
        var intent = MakanApp.Domain.Platform.PlatformProvisioningRequest.AwaitSchoolManagerPolicy(app, order, Guid.NewGuid(), "مدرسه", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(app, intent.SchoolApplicationId); Assert.Equal(order, intent.SourceOrderId);
        Assert.Equal("INITIAL_SCHOOL_MANAGER_POLICY_REQUIRED", intent.FailureCode); Assert.Null(intent.InitialManagerUserId);
        Assert.Null(intent.OrganizationId); Assert.Null(intent.SubscriptionId);
        Assert.Throws<InvalidOperationException>(() => intent.Resume(DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => intent.Complete(DateTime.UtcNow));
    }
}
