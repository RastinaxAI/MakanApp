using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class PersonalAcademicModelTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void PersonalAssignmentUsesRealClassAndMembershipWithoutOrganization()
    {
        var classId = Guid.NewGuid(); var memberId = Guid.NewGuid();
        var assignment = Assignment.CreateDraft(AcademicScope.Personal(classId), classId, memberId, Now);
        Assert.Null(assignment.OrganizationId); Assert.Null(assignment.CreatedByMembershipId);
        Assert.Equal(memberId, assignment.CreatedByPersonalMembershipId); Assert.Equal(classId, assignment.ContextId);
        Assert.Throws<ArgumentException>(() => Assignment.CreateDraft(AcademicScope.Personal(classId), Guid.NewGuid(), memberId, Now));
    }
    [Fact]
    public void AcademicGrantRevocationIsIdempotentAndDoesNotMutateGrantedCapabilities()
    {
        var grant = PersonalClassAcademicGrant.Create(Guid.NewGuid(), Guid.NewGuid(), PersonalClassAcademicCapabilities.Grade,
            null, Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), Now);
        Assert.True(grant.Allows(PersonalClassAcademicCapabilities.Grade));
        Assert.False(grant.Allows(PersonalClassAcademicCapabilities.AuthorExams));
        grant.Revoke(Now.AddMinutes(1)); grant.Revoke(Now.AddMinutes(2));
        Assert.Equal(Now.AddMinutes(1), grant.RevokedAtUtc); Assert.False(grant.Allows(PersonalClassAcademicCapabilities.Grade));
        Assert.Equal(PersonalClassAcademicCapabilities.Grade, grant.Capabilities);
    }
    [Fact]
    public void PublishedAssignmentAttachmentSnapshotCannotBeChanged()
    {
        var id = Guid.NewGuid(); var file = Guid.NewGuid();
        var version = AssignmentVersion.CreateDraft(AcademicScope.Personal(id), id, Guid.NewGuid(), 1, "عنوان", "توضیح",
            Now.AddDays(1), false, 2, 20, Guid.NewGuid(), Now);
        version.ReplaceAttachments([file], Now); version.Publish(Now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => version.ReplaceAttachments([], Now.AddMinutes(2)));
        Assert.Equal(file, Assert.Single(version.Attachments).FileAssetId);
    }
    [Fact]
    public void DeadlineFinalizationUsesServerDeadlineAndHasNoFabricatedSessionOrOperation()
    {
        var id = Guid.NewGuid(); var attempt = ExamAttempt.Start(AcademicScope.Personal(id), Guid.NewGuid(), Guid.NewGuid(), id,
            new(AcademicClassKind.PersonalClass, Guid.NewGuid()), Guid.NewGuid(), 1, 2, Now, Now.AddHours(1), 10, Now);
        Assert.Throws<InvalidOperationException>(() => attempt.FinalizeAtDeadline(Now.AddMinutes(9)));
        attempt.AcceptAnswerMutation(); attempt.FinalizeAtDeadline(Now.AddMinutes(15));
        Assert.Equal(Now.AddMinutes(10), attempt.FinalizedAtUtc); Assert.Equal(Now.AddMinutes(15), attempt.AutoFinalizedAtUtc);
        Assert.Equal(1, attempt.FinalizedAnswerSetVersion); Assert.Null(attempt.FinalizedBySessionId);
        Assert.Null(attempt.FinalizeClientOperationId); Assert.Null(attempt.FinalizeRequestHash);
        Assert.Throws<ExamAttemptNotWritableException>(() => attempt.EnsureWritable(Now.AddMinutes(15)));
        Assert.Throws<InvalidOperationException>(() => attempt.FinalizeAtDeadline(Now.AddMinutes(16)));
    }
}
