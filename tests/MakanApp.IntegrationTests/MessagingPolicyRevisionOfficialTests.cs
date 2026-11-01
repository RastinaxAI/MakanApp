using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
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

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task Policy20261101OfficialPairVersionTracksCurrentRestrictionWithoutRevokingHistoricalRead()
    {
        using var scenario = await CreateOfficialScenarioAsync();
        var route = $"/api/v1/organization/classes/{scenario.ClassId}/official-spaces";
        var before = (await scenario.Manager.GetFromJsonAsync<OfficialSpacesPairResult>(route, JsonOptions))!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.Users.SingleAsync(u => u.Id == scenario.ManagerId)).RestrictMessaging(DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
            await db.SaveChangesAsync();
        }
        var after = (await scenario.Manager.GetFromJsonAsync<OfficialSpacesPairResult>(route, JsonOptions))!;
        Assert.NotEqual(before.Version, after.Version);
        foreach (var destination in new[] { after.Group, after.Channel })
        {
            Assert.True(destination.Capabilities.CanRead);
            Assert.False(destination.EffectiveCapabilities.CanPublish);
            Assert.False(destination.EffectiveCapabilities.CanManageMessages);
        }
    }

    [Fact]
    public async Task Policy20261101RepairOfExistingBindingInvalidatesCurrentAudienceAfterReady()
    {
        using var scenario = await CreateOfficialScenarioAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var provision = await db.OfficialSpaceProvisions.SingleAsync(p => p.ClassId == scenario.ClassId && p.Purpose == ConversationType.Group);
        provision.RequireRepair("BindingConflict"); await db.SaveChangesAsync();
        var before = await db.OutboxMessages.Select(e => e.Id).ToArrayAsync();
        Assert.True(await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().EnsurePairAsync(scenario.OrganizationId, scenario.ClassId, default));
        var changes = await db.OutboxMessages.AsNoTracking().Where(e => !before.Contains(e.Id) && e.MessageType == MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter.MessageType).ToArrayAsync();
        Assert.Contains(changes, e => e.Payload.Contains(scenario.ManagerId.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.All(changes, e => Assert.DoesNotContain("text", e.Payload, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(OfficialSpaceState.Ready, await db.OfficialSpaceProvisions.Where(p => p.Id == provision.Id).Select(p => p.State).SingleAsync());
        Assert.Equal(2, await db.Conversations.CountAsync(c => c.AcademicClassId == scenario.ClassId));
    }

    [Fact]
    public async Task Policy20261101TwoTeachersAndManagerHaveOfficialContentCapabilitiesWithoutOwningRoster()
    {
        using var scenario = await CreateOfficialScenarioAsync(); using var secondClient = factory.CreateClient();
        var second = await CreateMessagingUserAsync(secondClient);
        var member = await factory.CreateMembershipAsync(second.UserId, scenario.OrganizationId, OrganizationRole.Teacher);
        var assignment = await factory.CreateTeacherAssignmentAsync(scenario.OrganizationId, scenario.ClassId, member.MembershipId);
        var person = await factory.CreateOrganizationPersonForUserAsync(scenario.OrganizationId, second.UserId);
        var enrollment = await factory.CreateEnrollmentAsync(scenario.OrganizationId, scenario.ClassId, person);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.RoleAssignments.AddRange(RoleAssignment.CreateActive(member.MembershipId, OrganizationRole.Parent, DateTime.UtcNow),
                RoleAssignment.CreateActive(member.MembershipId, OrganizationRole.Student, DateTime.UtcNow));
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().EnsurePairAsync(scenario.OrganizationId, scenario.ClassId, default);
        }
        await SelectOrganizationAsync(secondClient, member.MembershipId, OrganizationRole.Teacher);
        foreach (var id in new[] { scenario.ManagerId, scenario.TeacherId, second.UserId }) await factory.SetCommunicationAgeCategoryAsync(id, CommunicationAgeCategory.Unknown);
        var personal = await CreateManagedAndReadAsync(secondClient, "groups", PersonalCommand("فضای شخصی مستقل معلم"));
        foreach (var client in new[] { scenario.Manager, scenario.Teacher, secondClient })
        {
            var pair = (await client.GetFromJsonAsync<OfficialSpacesPairResult>($"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
            foreach (var destination in new[] { pair.Group, pair.Channel })
            {
                Assert.True(destination.Capabilities.CanManage);
                var details = (await client.GetFromJsonAsync<ManagedConversationResult>($"/api/v1/conversations/{destination.ConversationId}", JsonOptions))!;
                Assert.Equal(ConversationManagementPolicy.SystemManagedAcademic, details.ManagementPolicy);
                Assert.True(details.EffectiveCapabilities.CanEditInfo); Assert.True(details.EffectiveCapabilities.CanManageMessages);
                Assert.True(details.EffectiveCapabilities.CanPublish); Assert.False(details.EffectiveCapabilities.CanManageMembers);
                Assert.False(details.EffectiveCapabilities.CanManageAdmins); Assert.False(details.EffectiveCapabilities.CanChangeSettings);
                Assert.All(details.Participants, p => Assert.Equal(ConversationParticipantRole.Member, p.Role));
                using var update = await client.PatchAsJsonAsync($"/api/v1/conversations/{details.ConversationId}/info",
                    new UpdateConversationInfoCommand(details.Title, "توضیح آزمایشی مجاز", details.Version), JsonOptions);
                update.EnsureSuccessStatusCode();
            }
        }
        var currentPair = (await scenario.Manager.GetFromJsonAsync<OfficialSpacesPairResult>($"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
        using (var rosterMutation = await scenario.Manager.DeleteAsync($"/api/v1/conversations/{currentPair.Group.ConversationId}/members/{scenario.StudentId}"))
            Assert.Equal(HttpStatusCode.Forbidden, rosterMutation.StatusCode);
        Assert.Equal(EnrollmentStatus.Active, await factory.GetEnrollmentStatusAsync(scenario.EnrollmentId));
        await factory.EndTeacherAssignmentAsync(assignment);
        var studentReason = (await secondClient.GetFromJsonAsync<ManagedConversationResult>($"/api/v1/conversations/{currentPair.Group.ConversationId}", JsonOptions))!;
        Assert.False(studentReason.EffectiveCapabilities.CanManageMessages); Assert.True(studentReason.EffectiveCapabilities.CanPublish);
        Assert.False((await secondClient.GetFromJsonAsync<ManagedConversationResult>($"/api/v1/conversations/{currentPair.Channel.ConversationId}", JsonOptions))!.EffectiveCapabilities.CanPublish);
        await factory.EndEnrollmentAsync(enrollment);
        using (var revoked = await secondClient.GetAsync($"/api/v1/conversations/{currentPair.Group.ConversationId}")) Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        (await secondClient.GetAsync($"/api/v1/conversations/{personal.ConversationId}")).EnsureSuccessStatusCode();
        await using var final = factory.Services.CreateAsyncScope(); var finalDb = final.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.True(await finalDb.RoleAssignments.AnyAsync(r => r.MembershipId == member.MembershipId && r.Role == OrganizationRole.Parent && r.Status == RoleAssignmentStatus.Active));
        Assert.Null((await finalDb.Conversations.SingleAsync(c => c.Id == currentPair.Group.ConversationId)).CreatedByUserId);
    }

    [Fact]
    public async Task Policy20261101OfficialSourceRenameUsesWorkerAndLeavesPersonalConversationUntouched()
    {
        using var scenario = await CreateOfficialScenarioAsync();
        var personal = await CreateManagedAndReadAsync(scenario.Teacher, "groups", PersonalCommand("عنوان شخصی محفوظ"));
        var before = (await scenario.Manager.GetFromJsonAsync<OfficialSpacesPairResult>($"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var classroom = await db.Classes.SingleAsync(c => c.Id == scenario.ClassId);
            db.Entry(classroom).Property(c => c.Title).CurrentValue = "نام تازهٔ کلاس آزمایشی";
            await scope.ServiceProvider.GetRequiredService<OfficialSpaceEventWriter>().CaptureSchoolAsync(default);
            await db.SaveChangesAsync();
        }
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().SynchronizeExistingAsync(scenario.OrganizationId, scenario.ClassId, default);
        var after = (await scenario.Manager.GetFromJsonAsync<OfficialSpacesPairResult>($"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
        Assert.Equal(before.Group.ConversationId, after.Group.ConversationId); Assert.Equal(before.Channel.ConversationId, after.Channel.ConversationId);
        Assert.Contains("نام تازهٔ کلاس آزمایشی", after.Group.Official!.DisplayLabel);
        Assert.Contains("نام تازهٔ کلاس آزمایشی", after.Channel.Official!.DisplayLabel);
        Assert.Equal(personal.Title, (await scenario.Teacher.GetFromJsonAsync<ManagedConversationResult>($"/api/v1/conversations/{personal.ConversationId}", JsonOptions))!.Title);
    }

    [Fact]
    public async Task Policy20261101SessionLifecycleUsesStablePublicationReceiptsAndDestinationDedupe()
    {
        using var scenario = await CreateOfficialScenarioAsync(); var start = DateTime.UtcNow.AddDays(5);
        using var create = await scenario.Teacher.PostAsJsonAsync($"/api/v1/academic/classes/{scenario.ClassId}/sessions",
            new CreateSessionCommand("جلسهٔ عمومی آزمایشی", start, start.AddHours(1), "UTC", null), JsonOptions);
        create.EnsureSuccessStatusCode(); var session = (await create.Content.ReadFromJsonAsync<SessionResult>(JsonOptions))!;
        Assert.NotNull(session.PublicationReceipt); Assert.Equal(AcademicActivityKind.Session, session.PublicationReceipt.ActivityKind);
        Assert.Equal(session.Id, session.PublicationReceipt.SourceId);
        var firstEvent = session.PublicationReceipt.EventId; await DeliverOfficialAsync(firstEvent); await DeliverOfficialAsync(firstEvent);
        using var update = await scenario.Teacher.PatchAsJsonAsync($"/api/v1/academic/sessions/{session.Id}",
            new UpdateSessionCommand(session.Title, start.AddHours(2), start.AddHours(3), "UTC", null, session.RowVersion), JsonOptions);
        update.EnsureSuccessStatusCode(); session = (await update.Content.ReadFromJsonAsync<SessionResult>(JsonOptions))!;
        Assert.NotEqual(firstEvent, session.PublicationReceipt!.EventId); Assert.Equal(2, session.PublicationReceipt.SourceRevision);
        await DeliverOfficialAsync(session.PublicationReceipt.EventId);
        using var cancel = await scenario.Teacher.PostAsJsonAsync($"/api/v1/academic/sessions/{session.Id}/cancel", new SessionVersionCommand(session.RowVersion), JsonOptions);
        cancel.EnsureSuccessStatusCode(); session = (await cancel.Content.ReadFromJsonAsync<SessionResult>(JsonOptions))!;
        Assert.Equal(3, session.PublicationReceipt!.SourceRevision); await DeliverOfficialAsync(session.PublicationReceipt.EventId); await DeliverOfficialAsync(session.PublicationReceipt.EventId);
        var pair = (await scenario.Student.GetFromJsonAsync<OfficialSpacesPairResult>($"/api/v1/organization/classes/{scenario.ClassId}/official-spaces", JsonOptions))!;
        var messages = (await scenario.Student.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{pair.Group.ConversationId}/messages", JsonOptions))!;
        Assert.Equal(3, messages.Messages.Count(m => m.AcademicActivity?.Kind == AcademicActivityKind.Session));
        var old = Assert.Single(messages.Messages, m => m.AcademicActivity?.EventId == firstEvent).AcademicActivity!;
        Assert.False(old.IsActionable); Assert.Equal("Superseded", old.State);
        var closed = Assert.Single(messages.Messages, m => m.AcademicActivity?.EventId == session.PublicationReceipt.EventId).AcademicActivity!;
        Assert.False(closed.IsActionable); Assert.Equal(SessionActivityAction.Cancelled, closed.SessionAction);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(3, await db.AcademicActivityPublications.CountAsync(p => p.SessionId == session.Id));
        Assert.Equal(6, await db.Messages.CountAsync(m => db.AcademicActivityPublications.Any(p => p.SessionId == session.Id && p.Id == m.AcademicActivityPublicationId)));
    }
}
