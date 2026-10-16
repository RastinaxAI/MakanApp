using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2OwnRestrictionStateIsPrivateAndReflectsCurrentExpiry()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var route = $"/api/v1/conversations/{id}/me/restriction"; var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var empty = (await setup.TargetClient.GetFromJsonAsync<MemberRestrictionResult>(route, JsonOptions))!;
            Assert.Equal(setup.Target.UserId, empty.UserId); Assert.False(empty.IsEffective); Assert.Null(empty.Version);
            var written = await RestrictP2MemberAsync(setup.OwnerClient, id, setup.Target.UserId, new(false, GroupMemberCapability.SendMedia, now.AddSeconds(20), "دلیل خصوصی مدیریت", null));
            var response = await setup.TargetClient.GetAsync(route); response.EnsureSuccessStatusCode();
            Assert.DoesNotContain("دلیل خصوصی مدیریت", await response.Content.ReadAsStringAsync());
            var own = (await response.Content.ReadFromJsonAsync<MemberRestrictionResult>(JsonOptions))!;
            Assert.Equal(written.Version, own.Version); Assert.True(own.IsEffective); Assert.Equal(GroupMemberCapability.SendMedia, own.DeniedCapabilities);
            Assert.Equal(HttpStatusCode.Forbidden, (await setup.TargetClient.GetAsync($"/api/v1/conversations/{id}/member-restrictions")).StatusCode);
            Assert.False((await setup.OwnerClient.GetFromJsonAsync<MemberRestrictionResult>(route, JsonOptions))!.IsEffective);
            factory.Clock.SetUtcNow(now.AddSeconds(21)); Assert.False((await setup.TargetClient.GetFromJsonAsync<MemberRestrictionResult>(route, JsonOptions))!.IsEffective);
            (await setup.OwnerClient.DeleteAsync($"/api/v1/conversations/{id}/members/{setup.Target.UserId}")).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.GetAsync(route)).StatusCode);
        }
        finally { factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task P2RestrictionExpiryBetweenValidationAndStoreReturnsBadRequestWithoutMutation()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        await using var realScope = factory.Services.CreateAsyncScope(); var advanced = false;
        var resolver = new P2AfterContextResolver(realScope.ServiceProvider.GetRequiredService<IAccessContextResolver>(), () => { advanced = true; factory.Clock.SetUtcNow(now.AddSeconds(2)); });
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>(); services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.RemoveAll<IAccessContextResolver>(); services.AddSingleton<IAccessContextResolver>(resolver);
        }));
        using var client = host.CreateClient(); UseBearerToken(client, setup.Owner.AccessToken);
        try
        {
            var response = await client.PutAsJsonAsync($"/api/v1/conversations/{setup.Conversation.ConversationId}/members/{setup.Target.UserId}/restriction", new SetMemberRestrictionCommand(true, GroupMemberCapability.None, now.AddSeconds(1), "انقضای حین اجرا", null), JsonOptions);
            Assert.True(advanced); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(MessagingErrorCodes.AdvancedManagementInvalid, await ReadProblemCodeAsync(response));
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.False(await db.ConversationMemberRestrictions.AnyAsync(r => r.ConversationId == setup.Conversation.ConversationId));
            Assert.Single(await factory.GetConversationParticipantsAsync(setup.Conversation.ConversationId), p => p.UserId == setup.Target.UserId && p.Status == ConversationParticipantStatus.Active);
        }
        finally { factory.Clock.UseSystemTime(); }
    }
    private sealed class P2AfterContextResolver(IAccessContextResolver inner, Action afterResolve) : IAccessContextResolver
    {
        public async Task<AccessContext> ResolveAsync(Guid userId, Guid sessionId, CancellationToken ct)
        {
            // مجوز از resolver واقعی می‌آید؛ فقط گذر ساعت میان validation و transaction کنترل می‌شود.
            var context = await inner.ResolveAsync(userId, sessionId, ct); afterResolve(); return context;
        }
    }
}
