using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AiChatEndpointsTests
{
    [Theory]
    [InlineData(OrganizationRole.Student)] [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Parent)] [InlineData(OrganizationRole.Manager)]
    public async Task UnmeteredOrganizationRoleWithoutSubscriptionInvokesAndRevokedMembershipCannotInvoke(OrganizationRole role)
    {
        using var setup = factory.CreateClient(); var user = await CreateAuthenticatedUserAsync(setup); UseBearerToken(setup, user.AccessToken);
        using (var profile = await setup.PatchAsJsonAsync("/api/v1/me/profile", new CompleteProfileCommand("QA", "AI", "QA AI", "qa" + Guid.NewGuid().ToString("N")[..20]), JsonOptions))
            profile.EnsureSuccessStatusCode();
        var organization = await factory.CreateOrganizationAsync(NewName("بدون قرارداد AI"));
        var member = await factory.CreateMembershipAsync(user.User.Id, organization, role);
        var classroom = await factory.CreateAcademicClassAsync(organization);
        Guid? subject = null;
        if (role == OrganizationRole.Student)
        {
            subject = await factory.CreateOrganizationPersonAsync(organization, await factory.GetUserPersonIdAsync(user.User.Id));
            await factory.CreateEnrollmentAsync(organization, classroom.ClassId, subject.Value);
        }
        if (role == OrganizationRole.Teacher) await factory.CreateTeacherAssignmentAsync(organization, classroom.ClassId, member.MembershipId);
        if (role == OrganizationRole.Parent)
        {
            subject = await factory.CreateOrganizationPersonAsync(organization, await factory.CreatePersonWithoutUserAsync());
            await factory.CreateGuardianRelationAsync(user.User.Id, organization, subject.Value);
            await factory.CreateEnrollmentAsync(organization, classroom.ClassId, subject.Value);
        }
        await SelectUnmeteredRoleAsync(setup, member.MembershipId, role);
        if (role == OrganizationRole.Parent)
        {
            using var selected = await setup.PostAsync($"/api/v1/guardian/children/{subject}/select", null); selected.EnsureSuccessStatusCode();
        }
        await SelectAssistantFromWorkspaceAsync(setup);
        using var host = factory.WithAiUsageMode(AiUsageMode.Unmetered); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = setup.DefaultRequestHeaders.Authorization;
        var endpoint = role switch
        {
            OrganizationRole.Student => "/api/v1/ai/students/analyze",
            OrganizationRole.Teacher => $"/api/v1/ai/teachers/classes/{classroom.ClassId}/summary",
            OrganizationRole.Parent => "/api/v1/ai/parents/child-summary",
            _ => "/api/v1/ai/managers/school-summary"
        };
        var operation = Guid.NewGuid(); client.DefaultRequestHeaders.Add("X-AI-Operation-Id", operation.ToString("D"));
        using (var accepted = await client.PostAsync(endpoint, null)) accepted.EnsureSuccessStatusCode();
        Assert.Equal(1, factory.AiService.ProviderCallCount);
        Assert.Equal(role.ToString(), factory.AiService.LastRoleRequest!.Actor.Role);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.False(await db.Subscriptions.AnyAsync(s => s.OwnerId == organization || s.OwnerId == user.User.Id));
            var reservation = await db.UsageReservations.SingleAsync(r => r.OperationId == operation);
            Assert.True(reservation.IsUnmetered); Assert.False(reservation.IsTrial); Assert.Null(reservation.SubscriptionId);
            Assert.Equal(CommerceContextType.Organization, reservation.ContextType); Assert.Equal(organization, reservation.ContextId);
            (await db.Memberships.SingleAsync(m => m.Id == member.MembershipId)).End(DateTime.UtcNow); await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Remove("X-AI-Operation-Id");
        using var denied = await client.PostAsync(endpoint, null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.Equal(1, factory.AiService.ProviderCallCount);
    }

    [Theory]
    [InlineData(OrganizationRole.Teacher, false)] [InlineData(OrganizationRole.Teacher, true)]
    [InlineData(OrganizationRole.Manager, false)] [InlineData(OrganizationRole.Manager, true)]
    public async Task UnmeteredForeignClassAndUnenrolledStudentAreDeniedBeforeReservation(OrganizationRole role, bool foreignClass)
    {
        using var setup = factory.CreateClient(); var user = await CreateAuthenticatedUserAsync(setup); UseBearerToken(setup, user.AccessToken);
        var organization = await factory.CreateOrganizationAsync(NewName("کلاس مجاز نامحدود"));
        var foreign = await factory.CreateOrganizationAsync(NewName("کلاس بیگانه نامحدود"));
        var member = await factory.CreateMembershipAsync(user.User.Id, organization, role);
        var classroom = await factory.CreateAcademicClassAsync(organization); var other = await factory.CreateAcademicClassAsync(foreign);
        var learner = await factory.CreateOrganizationPersonAsync(organization, await factory.CreatePersonWithoutUserAsync());
        if (role == OrganizationRole.Teacher) await factory.CreateTeacherAssignmentAsync(organization, classroom.ClassId, member.MembershipId);
        await SelectUnmeteredRoleAsync(setup, member.MembershipId, role); await SelectAssistantFromWorkspaceAsync(setup);
        using var host = factory.WithAiUsageMode(AiUsageMode.Unmetered); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = setup.DefaultRequestHeaders.Authorization;
        var operation = Guid.NewGuid(); client.DefaultRequestHeaders.Add("X-AI-Operation-Id", operation.ToString("D"));
        var prefix = role == OrganizationRole.Teacher ? "teachers" : "managers";
        using var rejected = await client.PostAsync($"/api/v1/ai/{prefix}/classes/{(foreignClass ? other.ClassId : classroom.ClassId)}/students/{learner}/insight", null);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode); Assert.Equal(0, factory.AiService.ProviderCallCount);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().UsageReservations.AnyAsync(r => r.OperationId == operation));
    }

    // برخلاف helper قدیمی این انتخاب هیچ قرارداد تجاریِ ساختگی ایجاد نمی‌کند.
    private static async Task SelectUnmeteredRoleAsync(HttpClient client, Guid membership, OrganizationRole role)
    {
        using var selected = await client.PostAsJsonAsync("/api/v1/workspaces/select", new SelectWorkspaceCommand(WorkspaceType.Organization, membership, role), JsonOptions);
        selected.EnsureSuccessStatusCode();
    }
}
