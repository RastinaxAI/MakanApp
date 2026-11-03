using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    private const PersonalClassAcademicCapabilities AllAcademic = PersonalClassAcademicCapabilities.AuthorAssignments |
        PersonalClassAcademicCapabilities.AuthorExams | PersonalClassAcademicCapabilities.Grade;

    [Fact]
    public async Task PersonalAcademicManagerNeedsExplicitGrantAndPublishesOnlyToActiveLocalLearners()
    {
        var host = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var classroom = await ApprovedClassAsync(host, "کلاس آموزشی بدون مدرس");
        var command = AssignmentDraft();
        using var beforeGrant = await host.Client.PostAsJsonAsync($"/api/v1/academic/personal-classes/{classroom.Id}/assignments", command, Json);
        Assert.Equal(HttpStatusCode.NotFound, beforeGrant.StatusCode);
        var admin = await AdminAsync();
        var grant = await GrantAcademicAsync(admin, classroom.Id, host, AllAcademic);
        Assert.True(grant.IsEffective); Assert.Null(grant.ParentGrantId);
        classroom = await GetAsync<PersonalClassDto>(host.Client, $"/api/v1/personal-classes/{classroom.Id}");
        Assert.True(classroom.Capabilities.CanAuthorAssignments); Assert.True(classroom.Capabilities.CanAuthorExams);
        Assert.True(classroom.Capabilities.CanGrade); Assert.False(classroom.Capabilities.CanViewOwnResults);
        var learner = await ActorAsync(); var outsider = await ActorAsync();
        await JoinAcademicAsync(host, learner, classroom.Id, PersonalClassLocalRoles.Learner);
        var draft = await PostAsync<AssignmentResult>(host.Client, $"/api/v1/academic/personal-classes/{classroom.Id}/assignments", command);
        Assert.Null(draft.OrganizationId); Assert.Equal(AcademicClassKind.PersonalClass, draft.ClassRef!.Kind);
        await using (var readScope = factory.Services.CreateAsyncScope())
            Assert.NotNull(await readScope.ServiceProvider.GetRequiredService<IAssignmentStore>()
                .GetAssignmentAsync(AcademicScope.Personal(classroom.Id), draft.Id, default));
        using var hidden = await learner.Client.GetAsync($"/api/v1/academic/assignments/{draft.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var published = await PostAsync<PublishAssignmentResult>(host.Client, $"/api/v1/academic/assignments/{draft.Id}/publish",
            new PublishAssignmentCommand(draft.AssignmentRowVersion, draft.VersionRowVersion));
        var replay = await PostAsync<PublishAssignmentResult>(host.Client, $"/api/v1/academic/assignments/{draft.Id}/publish",
            new PublishAssignmentCommand(draft.AssignmentRowVersion, draft.VersionRowVersion));
        Assert.Equal(1, published.RecipientCount); Assert.Equal(published.Assignment.VersionId, replay.Assignment.VersionId);
        var page = await GetAsync<AcademicPage<AssignmentResult>>(learner.Client, $"/api/v1/academic/personal-classes/{classroom.Id}/assignments?page=1&pageSize=1&published=true");
        Assert.Equal(1, page.Total); Assert.Equal(draft.Id, Assert.Single(page.Items).Id);
        await WithDb(async db =>
        {
            var recipient = await db.AssignmentRecipients.SingleAsync(r => r.AssignmentVersionId == draft.VersionId);
            var member = await db.PersonalClassParticipants.SingleAsync(m => m.PersonalClassId == classroom.Id && m.StudentUserId == learner.UserId && m.EndedAtUtc == null);
            Assert.Equal(member.Id, recipient.ParticipantId);
            Assert.Equal(classroom.Id, recipient.ClassId);
            Assert.Equal(AcademicClassKind.PersonalClass, recipient.ContextKind);
            Assert.Equal(classroom.Id, recipient.ContextId);
            Assert.Equal(MakanApp.Domain.Identity.UserAccountStatus.Active, (await db.Users.SingleAsync(u => u.Id == learner.UserId)).AccountStatus);
            Assert.Null((await db.Users.SingleAsync(u => u.Id == learner.UserId)).PersonId);
        });
        using var started = await learner.Client.PostAsJsonAsync($"/api/v1/academic/assignments/{draft.Id}/attempts", (object?)null, Json);
        Assert.True(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());
        var attempt = (await started.Content.ReadFromJsonAsync<SubmissionAttemptResult>(Json))!;
        Assert.Null(attempt.EnrollmentId); Assert.Null(attempt.OrganizationId);
        var resumed = await PostAsync<SubmissionAttemptResult>(learner.Client, $"/api/v1/academic/assignments/{draft.Id}/attempts", null);
        Assert.Equal(attempt.Id, resumed.Id);
        using var other = await outsider.Client.GetAsync($"/api/v1/academic/submission-attempts/{attempt.Id}");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        await WithDb(async db =>
        {
            var stored = await db.SubmissionAttempts.SingleAsync(a => a.Id == attempt.Id);
            Assert.NotNull(stored.PersonalClassMembershipId); Assert.Equal(classroom.Id, stored.ContextId);
            Assert.Equal(1, await db.AssignmentRecipients.CountAsync(r => r.AssignmentVersionId == draft.VersionId));
            Assert.False(await db.Classes.AnyAsync(c => c.Id == classroom.Id));
            Assert.False(await db.Enrollments.AnyAsync(e => e.ClassId == classroom.Id));
            Assert.False(await db.TeacherAssignments.AnyAsync(t => t.ClassId == classroom.Id));
            Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.TargetId == grant.Id.ToString()));
        });
    }

    private CreateAssignmentDraftCommand AssignmentDraft() => new("تکلیف مستقل", "راهنمای پاسخ", factory.Clock.GetUtcNow().UtcDateTime.AddDays(1), false, 2, 20m);

    private async Task<PersonalClassAcademicPermissionDto> GrantAcademicAsync(TestActor admin, Guid classId, TestActor target,
        PersonalClassAcademicCapabilities capabilities, string? expectedGrant = null)
    {
        var member = await WithDb(db => db.PersonalClassParticipants.AsNoTracking()
            .SingleAsync(m => m.PersonalClassId == classId && m.StudentUserId == target.UserId && m.EndedAtUtc == null));
        using var response = await admin.Client.PutAsJsonAsync($"/api/admin/v1/personal-classes/{classId}/academic-permissions/{member.Id}",
            new SetPersonalClassAcademicPermissionCommand(capabilities, Guid.NewGuid(), Convert.ToBase64String(member.RowVersion), expectedGrant, "اعطای صریح آزمون"), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PersonalClassAcademicPermissionDto>(Json))!;
    }

    private async Task JoinAcademicAsync(TestActor manager, TestActor target, Guid classId, PersonalClassLocalRoles roles)
    {
        var invitation = await PostAsync<PersonalClassInvitationDto>(manager.Client, $"/api/v1/personal-classes/{classId}/invitations",
            new InvitePersonalClassStudentCommand(target.UserId, Guid.NewGuid(), roles));
        await PostAsync<PersonalClassDto>(target.Client, $"/api/v1/personal-classes/invitations/{invitation.Id}/accept", null);
    }
}
