using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Organization;

public sealed class OrganizationOnboardingModelTests
{
    [Fact]
    public void ConsentedLinkChangesOnlySchoolReferenceAndDoesNotMergePersons()
    {
        var now = DateTime.UtcNow;
        var original = Person.Create("School", "Name", null, now);
        var account = Person.Create("Account", "Name", null, now);
        var school = OrganizationPerson.CreateActive(Guid.NewGuid(), original.Id, now);
        var id = school.Id;
        school.LinkConsentingAccountPerson(account.Id);
        Assert.Equal(id, school.Id);
        Assert.Equal(account.Id, school.PersonId);
        Assert.Equal("School", original.FirstName);
        Assert.NotEqual(original.Id, account.Id);
    }
    [Fact]
    public void EndedSchoolRecordCannotBeLinked()
    {
        var school = OrganizationPerson.CreateActive(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        school.End(DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => school.LinkConsentingAccountPerson(Guid.NewGuid()));
    }
    [Fact]
    public void LinkInvitationIsNarrowStudentInvitationWithExplicitTargetAndSchoolRecord()
    {
        var now = DateTime.UtcNow;
        var schoolId = Guid.NewGuid();
        var target = Guid.NewGuid();
        var operation = Guid.NewGuid();
        var invitation = Invitation.CreatePersonLink(Guid.NewGuid(), schoolId, target, Guid.NewGuid(), operation, now, now.AddDays(1));
        Assert.Equal(OrganizationRole.Student, invitation.Role);
        Assert.Equal(schoolId, invitation.OrganizationPersonId);
        Assert.Equal(target, invitation.DestinationUserId);
        Assert.Equal(operation, invitation.ClientOperationId);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
    }
    [Fact]
    public void CreationOperationAndFingerprintAreRequiredTogether()
    {
        Assert.Throws<ArgumentException>(() => OrganizationPerson.CreateOnboarded(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, new string('A', 64), DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => OrganizationPerson.CreateOnboarded(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "short", DateTime.UtcNow));
    }
}
