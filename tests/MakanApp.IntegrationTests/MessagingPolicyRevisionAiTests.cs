using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Identity;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AiChatEndpointsTests
{
    [Fact]
    public async Task Policy20261101CopilotWithoutDobUsesFakeProviderAndKeepsQuotaAndRoleBoundaries()
    {
        using var client = factory.CreateClient(); var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        using var profile = await client.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("Policy", "Copilot", "Policy Copilot", $"policy.ai.{Guid.NewGuid():N}"[..28]), JsonOptions);
        profile.EnsureSuccessStatusCode();
        var organization = await factory.CreateOrganizationAsync(NewName("Policy AI without DOB"));
        var membership = await factory.CreateMembershipAsync(user.User.Id, organization, OrganizationRole.Manager);
        await EnsureAiContractAsync(organization, limit: 1);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var account = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.User.Id);
            Assert.Equal(CommunicationAgeVerificationStatus.Unverified, account.CommunicationAgeVerificationStatus);
            Assert.Null(await db.Persons.Where(p => p.Id == account.PersonId).Select(p => p.DateOfBirth).SingleAsync());
            Assert.Single(await db.Memberships.Where(m => m.UserId == account.Id).ToArrayAsync());
        }
        var operation = Guid.NewGuid();
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat"))
        {
            request.Content = JsonContent.Create(new SendAiChatCommand("پرسش عمومی ساختگی بدون سن", null) { OperationId = operation }, options: JsonOptions);
            using var response = await client.SendAsync(request); response.EnsureSuccessStatusCode();
        }
        Assert.Equal(1, factory.AiService.ProviderCallCount);
        using (var exhausted = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat"))
        {
            exhausted.Content = JsonContent.Create(new SendAiChatCommand("پرسش ساختگی بعد از مصرف سهمیه", null) { OperationId = Guid.NewGuid() }, options: JsonOptions);
            using var response = await client.SendAsync(exhausted); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        Assert.Equal(1, factory.AiService.ProviderCallCount);
        await using var final = factory.Services.CreateAsyncScope(); var finalDb = final.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await finalDb.UsageLedgerEntries.CountAsync(e => e.SourceOperationId == operation && e.Status == UsageReservationStatus.Consumed));
        Assert.Equal(membership.MembershipId, Assert.Single(await finalDb.Memberships.Where(m => m.UserId == user.User.Id).ToArrayAsync()).Id);
    }
}
