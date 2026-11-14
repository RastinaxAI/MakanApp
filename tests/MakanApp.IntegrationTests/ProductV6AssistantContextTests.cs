using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Ai;
using MakanApp.Domain.Commerce;
using MakanApp.Application.Organization;
using MakanApp.Domain.Intelligence;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task ProductV6AiExecutionKeepsAssistantDataPayerAndSchoolWorkspaceIndependent()
    {
        factory.AiService.Reset();
        var actor = await ActorAsync(); var a = await factory.CreateOrganizationAsync("دادهٔ Assistant"); var b = await factory.CreateOrganizationAsync("Workspace مدرسه");
        var manager = await factory.CreateMembershipAsync(actor.UserId, a, OrganizationRole.Manager);
        var teacher = await factory.CreateMembershipAsync(actor.UserId, b, OrganizationRole.Teacher);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual));
        using var chosen = await actor.Client.PutAsJsonAsync("/api/v1/ai/assistant-context",
            new SelectAssistantContextCommand(AssistantDataContextKind.Organization, Guid.NewGuid(), manager.MembershipId, OrganizationRole.Manager), Json);
        chosen.EnsureSuccessStatusCode(); await SelectAsync(actor, teacher.MembershipId, OrganizationRole.Teacher);
        var operation = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat") {
            Content = JsonContent.Create(new SendAiChatCommand("پرسش ساختگی تست زمینه", null) { OperationId = operation }, options: Json)
        };
        request.Headers.Add("X-AI-Payer", "Individual");
        using var response = await actor.Client.SendAsync(request); response.EnsureSuccessStatusCode();
        Assert.Equal(a.ToString("D"), factory.AiService.LastRequest!.Scope.OrganizationId); Assert.Equal("Manager", factory.AiService.LastRequest.Actor.Role);
        await WithDb(async db => {
            var reservation = await db.UsageReservations.SingleAsync(r => r.OperationId == operation);
            Assert.Equal(SubscriptionOwnerType.Individual, reservation.OwnerType); Assert.Equal(actor.UserId, reservation.OwnerId);
            Assert.Equal(a, reservation.ContextId); Assert.Equal(UsageReservationStatus.Consumed, reservation.Status);
        });
        var workspace = await GetAsync<AccessContext>(actor.Client, "/api/v1/workspaces/current"); Assert.Equal(b, workspace.OrganizationId);
        factory.AiService.Reset();
    }

    [Fact]
    public async Task ProductV6AssistantContextIsPinnedAcrossSchoolRoleSwitchAndRevalidatesRevocation()
    {
        var actor = await ActorAsync(); var a = await factory.CreateOrganizationAsync("زمینه الف"); var b = await factory.CreateOrganizationAsync("زمینه ب");
        var first = await factory.CreateMembershipAsync(actor.UserId, a, OrganizationRole.Manager);
        var second = await factory.CreateMembershipAsync(actor.UserId, b, OrganizationRole.Teacher);
        await SelectAsync(actor, first.MembershipId, OrganizationRole.Manager);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var pinned = await scope.ServiceProvider.GetRequiredService<AssistantContextResolver>().ResolveAsync(actor.UserId, actor.SessionId, default);
            Assert.Equal(a, pinned.OrganizationId); Assert.Equal(OrganizationRole.Manager, pinned.ActiveRole);
        }
        await SelectAsync(actor, second.MembershipId, OrganizationRole.Teacher);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var pinned = await scope.ServiceProvider.GetRequiredService<AssistantContextResolver>().ResolveAsync(actor.UserId, actor.SessionId, default);
            var school = await scope.ServiceProvider.GetRequiredService<IAccessContextResolver>().ResolveAsync(actor.UserId, actor.SessionId, default);
            Assert.Equal(a, pinned.OrganizationId); Assert.Equal(b, school.OrganizationId);
        }
        await WithDb(async db => { var membership = await db.Memberships.SingleAsync(m => m.Id == first.MembershipId); membership.Suspend(); await db.SaveChangesAsync(); });
        using var revoked = await actor.Client.GetAsync("/api/v1/ai/assistant-context"); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        await WithDb(async db => Assert.Equal(a, (await db.AssistantContextSelections.SingleAsync(s => s.SessionId == actor.SessionId)).OrganizationId));
        var selected = await GetAsync<AccessContext>(actor.Client, "/api/v1/workspaces/current");
        Assert.Equal(b, selected.OrganizationId);
    }

    [Fact]
    public async Task ProductV6AssistantSelectionCannotGrantForeignRoleOrSilentlyMutateSchoolWorkspace()
    {
        var actor = await ActorAsync(); var other = await ActorAsync(); var org = await factory.CreateOrganizationAsync("آزمون زمینه صریح");
        var own = await factory.CreateMembershipAsync(actor.UserId, org, OrganizationRole.Manager);
        var foreign = await factory.CreateMembershipAsync(other.UserId, org, OrganizationRole.Manager);
        await SelectAsync(actor, own.MembershipId, OrganizationRole.Manager);
        using var denied = await actor.Client.PutAsJsonAsync("/api/v1/ai/assistant-context",
            new SelectAssistantContextCommand(AssistantDataContextKind.Organization, Guid.NewGuid(), foreign.MembershipId, OrganizationRole.Manager), Json);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var selected = await actor.Client.PutAsJsonAsync("/api/v1/ai/assistant-context", new SelectAssistantContextCommand(AssistantDataContextKind.Personal, Guid.NewGuid()), Json);
        selected.EnsureSuccessStatusCode(); var result = (await selected.Content.ReadFromJsonAsync<AssistantContextResult>(Json))!;
        Assert.True(result.UpstreamContextSupported); Assert.Null(result.Limitation);
        await using var scope = factory.Services.CreateAsyncScope();
        var assistant = await scope.ServiceProvider.GetRequiredService<AssistantContextResolver>().ResolveAsync(actor.UserId, actor.SessionId, default);
        var school = await scope.ServiceProvider.GetRequiredService<IAccessContextResolver>().ResolveAsync(actor.UserId, actor.SessionId, default);
        Assert.Equal(WorkspaceType.Personal, assistant.WorkspaceType); Assert.Equal(org, school.OrganizationId);
    }

    [Fact]
    public async Task ProductV6AssistantClassSelectionRejectsOtherClassEvenWhenManagerCanAccessBoth()
    {
        var actor = await ActorAsync(); var org = await factory.CreateOrganizationAsync("آزمون کلاس Assistant");
        var member = await factory.CreateMembershipAsync(actor.UserId, org, OrganizationRole.Manager);
        var a = await factory.CreateAcademicClassAsync(org); var b = await factory.CreateAcademicClassAsync(org);
        using var selected = await actor.Client.PutAsJsonAsync("/api/v1/ai/assistant-context",
            new SelectAssistantContextCommand(AssistantDataContextKind.OrganizationClass, Guid.NewGuid(), member.MembershipId, OrganizationRole.Manager, ClassId: a.ClassId), Json);
        selected.EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        var builder = scope.ServiceProvider.GetRequiredService<IManagerClassContextBuilder>();
        var allowed = await builder.BuildAsync(actor.UserId, actor.SessionId, a.ClassId, default); Assert.Equal(a.ClassId.ToString("D"), allowed.Scope.ClassId);
        var error = await Assert.ThrowsAsync<AiServiceException>(() => builder.BuildAsync(actor.UserId, actor.SessionId, b.ClassId, default));
        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, error.Code);
    }
}
