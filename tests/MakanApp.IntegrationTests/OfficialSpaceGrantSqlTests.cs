using MakanApp.Application.Messaging;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class OfficialSpaceGrantSqlTests(MakanAppWebApplicationFactory factory)
{
    [Fact]
    public async Task SourceBackedClassAudienceMatchesTeacherAndStudentAndAddsOnlyAuthorizedManager()
    {
        var f = await SeedAsync();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        foreach (var conversation in new[] { f.GroupId, f.ChannelId })
        {
            var grants = await OfficialSpaceAccessQuery.Conversations(db).Where(g => g.ConversationId == conversation).ToArrayAsync();
            Assert.Equal(new[] { f.ManagerId, f.TeacherId, f.StudentId }.Order(), grants.Select(g => g.UserId).Distinct().Order());
            Assert.DoesNotContain(grants, g => g.UserId == f.ParentId || g.UserId == f.ForeignManagerId);
            Assert.All(grants.Where(g => g.UserId == f.ManagerId || g.UserId == f.TeacherId), g => Assert.True(g.CanPublish));
            Assert.Equal(conversation == f.GroupId, grants.Single(g => g.UserId == f.StudentId).CanPublish);
        }
        foreach (var conversation in new[] { f.OrganizationGroupId, f.OrganizationChannelId })
        {
            var ids = await OfficialSpaceAccessQuery.Conversations(db).Where(g => g.ConversationId == conversation).Select(g => g.UserId).Distinct().ToArrayAsync();
            Assert.Equal(f.ManagerId, Assert.Single(ids));
        }
        Assert.Empty(await OfficialSpaceAccessQuery.SourceReasons(db, f.ForeignOrganizationId, f.ClassId).Where(r => r.UserId == f.TeacherId).ToArrayAsync());
    }

    [Theory]
    [InlineData("TeacherAssignment")]
    [InlineData("TeacherRole")]
    [InlineData("TeacherMembership")]
    [InlineData("TeacherAccount")]
    [InlineData("Enrollment")]
    [InlineData("StudentPerson")]
    [InlineData("Class")]
    [InlineData("Organization")]
    public async Task RevokedSourceReasonIsDeniedBeforeAnyParticipantSync(string ended)
    {
        var f = await SeedAsync();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var now = DateTime.UtcNow;
        var user = ended is "Enrollment" or "StudentPerson" ? f.StudentId : f.TeacherId;
        switch (ended)
        {
            case "TeacherAssignment": (await db.TeacherAssignments.SingleAsync(t => t.Id == f.TeacherAssignmentId)).End(now); break;
            case "TeacherRole": (await db.RoleAssignments.SingleAsync(r => r.Id == f.TeacherRoleId)).End(now); break;
            case "TeacherMembership": (await db.Memberships.SingleAsync(m => m.Id == f.TeacherMembershipId)).End(now); break;
            case "TeacherAccount": (await db.Users.SingleAsync(u => u.Id == f.TeacherId)).Suspend(now); break;
            case "Enrollment": (await db.Enrollments.SingleAsync(e => e.Id == f.EnrollmentId)).Withdraw(now); break;
            case "StudentPerson": (await db.OrganizationPersons.SingleAsync(p => p.Id == f.StudentPersonId)).End(now); break;
            case "Class": (await db.Classes.SingleAsync(c => c.Id == f.ClassId)).Archive(now); break;
            case "Organization": (await db.Organizations.SingleAsync(o => o.Id == f.OrganizationId)).Suspend(); break;
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.False(await OfficialSpaceAccessQuery.Conversations(db).AnyAsync(g => g.UserId == user && (g.ConversationId == f.GroupId || g.ConversationId == f.ChannelId)));
        Assert.True(await db.ConversationParticipants.AnyAsync(p => p.ConversationId == f.GroupId && p.UserId == user && p.Status == ConversationParticipantStatus.Active));
    }

    [Fact]
    public async Task IndependentManagerReasonSurvivesTeacherAssignmentEnd()
    {
        var f = await SeedAsync(); await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var now = DateTime.UtcNow;
        var managerRole = RoleAssignment.CreateActive(f.TeacherMembershipId, OrganizationRole.Manager, now); db.RoleAssignments.Add(managerRole);
        (await db.TeacherAssignments.SingleAsync(t => t.Id == f.TeacherAssignmentId)).End(now); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.True(await OfficialSpaceAccessQuery.Conversations(db).AnyAsync(g => g.ConversationId == f.GroupId && g.UserId == f.TeacherId));
        var reasons = await OfficialSpaceAccessQuery.SourceReasons(db, f.OrganizationId, f.ClassId).Where(r => r.UserId == f.TeacherId).ToArrayAsync();
        Assert.Equal(OfficialAccessReasonKind.ManagerRole, Assert.Single(reasons).Kind);
        (await db.RoleAssignments.SingleAsync(r => r.Id == managerRole.Id)).End(now); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.False(await OfficialSpaceAccessQuery.Conversations(db).AnyAsync(g => g.ConversationId == f.GroupId && g.UserId == f.TeacherId));
        Assert.True(await OfficialSpaceAccessQuery.Conversations(db).AnyAsync(g => g.ConversationId == f.GroupId && g.UserId == f.StudentId));
    }

    [Fact]
    public async Task UserManagedOfficialLookingTitleNeverGetsBindingOrGrant()
    {
        var f = await SeedAsync(); await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var normal = Conversation.CreateUserManaged(ConversationType.Group, ConversationScope.Organization, f.OrganizationId,
            "گروه رسمی جعلی", null, f.ManagerId, Guid.NewGuid(), new byte[32], DateTime.UtcNow);
        db.Conversations.Add(normal); await db.SaveChangesAsync();
        Assert.Null(OfficialSpaceMetadata.From(normal));
        Assert.False(await OfficialSpaceAccessQuery.Conversations(db).AnyAsync(g => g.ConversationId == normal.Id));
    }

    private async Task<Fixture> SeedAsync()
    {
        var organization = await factory.CreateOrganizationAsync("سازمان آزمون SQL رسمی " + Guid.NewGuid().ToString("N"));
        var foreignOrganization = await factory.CreateOrganizationAsync("سازمان مستقل " + Guid.NewGuid().ToString("N"));
        var classroom = await factory.CreateAcademicClassAsync(organization);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var now = DateTime.UtcNow;
        User Account()
        {
            var person = Person.Create("کاربر", "آزمایشی", null, now); var account = User.Create(now); var name = "official." + Guid.NewGuid().ToString("N")[..12];
            account.SetProfile(person.Id, name, name.ToUpperInvariant(), now); db.Persons.Add(person); db.Users.Add(account); return account;
        }
        (Membership Membership, RoleAssignment Role) Member(User account, OrganizationRole role, Guid org)
        {
            var membership = Membership.CreateActive(account.Id, org, now); var assignment = RoleAssignment.CreateActive(membership.Id, role, now);
            db.Memberships.Add(membership); db.RoleAssignments.Add(assignment); return (membership, assignment);
        }
        var manager = Account(); var teacher = Account(); var student = Account(); var parent = Account(); var outsider = Account();
        _ = Member(manager, OrganizationRole.Manager, organization); var teacherMembership = Member(teacher, OrganizationRole.Teacher, organization);
        _ = Member(student, OrganizationRole.Student, organization); _ = Member(parent, OrganizationRole.Parent, organization); _ = Member(outsider, OrganizationRole.Manager, foreignOrganization);
        var learner = OrganizationPerson.CreateActive(organization, student.PersonId!.Value, now); db.OrganizationPersons.Add(learner);
        var enrollment = Enrollment.CreateActive(organization, classroom.ClassId, learner.Id, now); db.Enrollments.Add(enrollment);
        var assignment = TeacherAssignment.CreateActive(organization, classroom.ClassId, teacherMembership.Membership.Id, now); db.TeacherAssignments.Add(assignment);
        Conversation ClassConversation(ConversationType purpose)
        { var c = Conversation.CreateSystemManagedAcademic(purpose, organization, "فضای رسمی آزمایشی", null, now); c.BindAcademicClass(classroom.ClassId); db.Conversations.Add(c); return c; }
        var group = ClassConversation(ConversationType.Group); var channel = ClassConversation(ConversationType.Channel);
        var orgGroup = Conversation.CreateOfficialOrganization(ConversationType.Group, organization, "گروه رسمی سازمان", now);
        var orgChannel = Conversation.CreateOfficialOrganization(ConversationType.Channel, organization, "کانال رسمی سازمان", now);
        db.Conversations.AddRange(orgGroup, orgChannel);
        foreach (var account in new[] { manager, teacher, student, parent, outsider })
            db.ConversationParticipants.Add(ConversationParticipant.CreateActive(group.Id, account.Id, now));
        await db.SaveChangesAsync();
        return new(organization, foreignOrganization, classroom.ClassId, manager.Id, teacher.Id, student.Id, parent.Id, outsider.Id,
            teacherMembership.Membership.Id, teacherMembership.Role.Id, assignment.Id, learner.Id, enrollment.Id, group.Id, channel.Id, orgGroup.Id, orgChannel.Id);
    }

    private sealed record Fixture(Guid OrganizationId, Guid ForeignOrganizationId, Guid ClassId, Guid ManagerId, Guid TeacherId,
        Guid StudentId, Guid ParentId, Guid ForeignManagerId, Guid TeacherMembershipId, Guid TeacherRoleId, Guid TeacherAssignmentId,
        Guid StudentPersonId, Guid EnrollmentId, Guid GroupId, Guid ChannelId, Guid OrganizationGroupId, Guid OrganizationChannelId);
}
