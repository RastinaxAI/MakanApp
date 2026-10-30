using System.Net;
using System.Text.Json;
using MakanApp.Application.Guardian;
using MakanApp.Application.Organization;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AccountRealtimeGuardianRevocationSendsOnlyOwnAccessHintAndInvalidatesChildCursor()
    {
        var setup = await CreateTeacherStudentSetupAsync();
        using var teacher = setup.TeacherClient; using var child = setup.StudentClient; using var parentClient = factory.CreateClient();
        var parent = await CreateMessagingUserAsync(parentClient);
        Guid organizationId; Guid learnerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var assignment = await db.TeacherAssignments.AsNoTracking().SingleAsync(t => t.Id == setup.TeacherAssignmentId);
            organizationId = assignment.OrganizationId;
            learnerId = await db.Enrollments.Where(e => e.ClassId == assignment.ClassId).Select(e => e.LearnerOrganizationPersonId).SingleAsync();
        }
        var membership = await factory.CreateMembershipAsync(parent.UserId, organizationId, MakanApp.Domain.Organization.OrganizationRole.Parent);
        var relationId = await factory.CreateGuardianRelationAsync(parent.UserId, organizationId, learnerId);
        await SelectOrganizationAsync(parentClient, membership.MembershipId, MakanApp.Domain.Organization.OrganizationRole.Parent);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IGuardianStore>();
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var sessionId = await db.UserSessions.Where(s => s.UserId == parent.UserId).Select(s => s.Id).SingleAsync();
            var session = (await store.GetSessionAsync(sessionId, parent.UserId, default))!;
            session.SelectSubjectOrganizationPerson(learnerId);
            await store.SaveChangesAsync(default);
        }
        var boundary = await AccountChangesAsync(parentClient);
        Guid id; string payload;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var oldIds = await db.OutboxMessages.Select(x => x.Id).ToArrayAsync();
            (await db.GuardianRelations.SingleAsync(r => r.Id == relationId)).Revoke(factory.Clock.GetUtcNow().UtcDateTime);
            await scope.ServiceProvider.GetRequiredService<IOrganizationPersonStore>().SaveAsync(default);
            var row = (await db.OutboxMessages.AsNoTracking().Where(x => x.MessageType == RealtimeInvalidationWriter.MessageType && !oldIds.Contains(x.Id)).ToArrayAsync())
                .Single(x => { using var json = JsonDocument.Parse(x.Payload); return json.RootElement.GetProperty("Key").GetString() == "access"; });
            id = row.Id; payload = row.Payload;
        }
        var recipients = await ReplayRecipientsAsync(id, payload);
        var hint = Assert.Single(recipients).Notification;
        Assert.Equal("access", hint.Key); Assert.Null(hint.ResourceId);
        var serialized = JsonSerializer.Serialize(hint);
        Assert.DoesNotContain(learnerId.ToString(), serialized);
        Assert.DoesNotContain(setup.Student.UserId.ToString(), serialized);
        using var stale = await parentClient.GetAsync("/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(boundary.NextCursor));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var problem = JsonDocument.Parse(await stale.Content.ReadAsStringAsync());
        Assert.Equal("REFETCH_AUTHORIZED_SNAPSHOTS", problem.RootElement.GetProperty("requiredAction").GetString());
        Assert.False(problem.RootElement.GetProperty("retryable").GetBoolean());
        await SelectPersonalAsync(parentClient);
        Assert.Empty((await AccountChangesAsync(parentClient)).Changes);
    }
}
