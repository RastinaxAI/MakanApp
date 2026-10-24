using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    [Fact]
    public async Task VerifiedPersonalAdultsDiscoverContactAndConcurrentlyCreateOneDirectWithoutSchool()
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var first = _host.CreateClient();
        using var second = _host.CreateClient();
        var a = await CreateP0PersonalAccountAsync(first, owner);
        var b = await CreateP0PersonalAccountAsync(second, owner);
        await AssertP0DiscoverableAsync(first, b.Username);
        await AssertP0DiscoverableAsync(second, a.Username);

        using var save = await first.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(b.UserId, "دوست"));
        save.EnsureSuccessStatusCode();
        var contact = (await save.Content.ReadFromJsonAsync<UserContactResult>(PolicyJson))!;
        using var repeat = await first.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(b.UserId, "جایگزین نشود"));
        repeat.EnsureSuccessStatusCode();
        Assert.True((await repeat.Content.ReadFromJsonAsync<UserContactResult>(PolicyJson))!.AlreadyExisted);
        Assert.Empty((await second.GetFromJsonAsync<UserContactPageResult>("/api/v1/contacts", PolicyJson))!.Items);
        using var rename = await first.PatchAsJsonAsync($"/api/v1/contacts/{b.UserId}", new UpdateUserContactCommand("دوست نزدیک", true, contact.Version));
        rename.EnsureSuccessStatusCode();
        using var stale = await first.PatchAsJsonAsync($"/api/v1/contacts/{b.UserId}", new UpdateUserContactCommand("قدیمی", false, contact.Version));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        var reloaded = Assert.Single((await first.GetFromJsonAsync<UserContactPageResult>("/api/v1/contacts", PolicyJson))!.Items);
        Assert.Equal("دوست نزدیک", reloaded.SavedName);
        Assert.True(reloaded.IsFavorite);

        var requests = Enumerable.Range(0, 6).Select(i => (i % 2 == 0 ? first : second).PostAsJsonAsync(
            "/api/v1/conversations/direct", new StartDirectConversationCommand(i % 2 == 0 ? b.UserId : a.UserId, ConversationScope.Personal), PolicyJson));
        var responses = await Task.WhenAll(requests);
        try
        {
            var conversations = new List<DirectConversationResult>();
            foreach (var response in responses)
            {
                response.EnsureSuccessStatusCode();
                conversations.Add((await response.Content.ReadFromJsonAsync<DirectConversationResult>(PolicyJson))!);
            }
            Assert.Single(conversations.Select(c => c.ConversationId).Distinct());
            Assert.Single(conversations, c => !c.AlreadyExisted);
            Assert.Equal(1, await factory.CountDirectConversationsAsync(a.UserId, b.UserId, ConversationScope.Personal, null));
            using var delete = await first.DeleteAsync($"/api/v1/contacts/{b.UserId}");
            delete.EnsureSuccessStatusCode();
            Assert.Empty((await first.GetFromJsonAsync<UserContactPageResult>("/api/v1/contacts", PolicyJson))!.Items);
            Assert.Equal(HttpStatusCode.OK, (await first.GetAsync($"/api/v1/conversations/{conversations[0].ConversationId}/messages")).StatusCode);
        }
        finally { foreach (var response in responses) response.Dispose(); }

        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await db.Memberships.AnyAsync(m => m.UserId == a.UserId || m.UserId == b.UserId));
        Assert.False(await db.PersonalCommunicationGrants.AnyAsync(g => g.LowerUserId == a.UserId || g.HigherUserId == a.UserId));
    }

    [Fact]
    public async Task UsernameDiscoveryNeverIncludesPhoneEvenWhenTargetMakesPhonePublic()
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var first = _host.CreateClient();
        using var second = _host.CreateClient();
        _ = await CreateP0PersonalAccountAsync(first, owner);
        var b = await CreateP0PersonalAccountAsync(second, owner);
        using var privacy = await second.PutAsJsonAsync("/api/v1/messaging/privacy", new UpdateMessagingPrivacyCommand(
            PrivacyAudience.Everyone, PrivacyAudience.Everyone, PrivacyAudience.Everyone, PrivacyAudience.Everyone, null), PolicyJson);
        privacy.EnsureSuccessStatusCode();
        var search = (await first.GetFromJsonAsync<PeopleSearchPageResult>($"/api/v1/people/search?q={b.Username}", PolicyJson))!;
        Assert.Null(Assert.Single(search.Items).PhoneNumber);
        using var username = await first.PostAsJsonAsync("/api/v1/people/resolve", new ResolvePersonCommand(b.Username, null));
        username.EnsureSuccessStatusCode();
        Assert.Null((await username.Content.ReadFromJsonAsync<PeopleResolveResult>(PolicyJson))!.Profile!.PhoneNumber);
        using var phone = await first.PostAsJsonAsync("/api/v1/people/resolve", new ResolvePersonCommand(null, b.Authentication.User.PhoneNumber));
        phone.EnsureSuccessStatusCode();
        Assert.NotNull((await phone.Content.ReadFromJsonAsync<PeopleResolveResult>(PolicyJson))!.Profile!.PhoneNumber);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtectedCallerCannotGloballyDiscoverUnrelatedAdult(bool verifiedMinor)
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var child = _host.CreateClient();
        using var adult = _host.CreateClient();
        var a = await CreateP0PersonalAccountAsync(child, verifiedMinor ? owner : null, 2015);
        var b = await CreateP0PersonalAccountAsync(adult, owner);
        Assert.Empty((await child.GetFromJsonAsync<PeopleSearchPageResult>($"/api/v1/people/search?q={b.Username}", PolicyJson))!.Items);
        using var resolve = await child.PostAsJsonAsync("/api/v1/people/resolve", new ResolvePersonCommand(b.Username, null));
        Assert.False((await resolve.Content.ReadFromJsonAsync<PeopleResolveResult>(PolicyJson))!.IsDiscoverable);
        Assert.Equal(HttpStatusCode.NotFound, (await child.GetAsync($"/api/v1/people/{b.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adult.GetAsync($"/api/v1/people/{a.UserId}")).StatusCode);
    }

    [Fact]
    public async Task RestrictedCallerCannotStartDirectAndDirectoryDoesNotAdvertiseThatPermission()
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var first = _host.CreateClient();
        using var second = _host.CreateClient();
        var a = await CreateP0PersonalAccountAsync(first, owner);
        var b = await CreateP0PersonalAccountAsync(second, owner);
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == a.UserId);
            user.RestrictMessaging(Now.AddHours(1), Now);
            await db.SaveChangesAsync();
        }
        using var resolve = await first.PostAsJsonAsync("/api/v1/people/resolve", new ResolvePersonCommand(b.Username, null));
        Assert.False((await resolve.Content.ReadFromJsonAsync<PeopleResolveResult>(PolicyJson))!.Profile!.CanStartDirect);
        using var direct = await first.PostAsJsonAsync("/api/v1/conversations/direct", new StartDirectConversationCommand(b.UserId, ConversationScope.Personal), PolicyJson);
        Assert.Equal(HttpStatusCode.Forbidden, direct.StatusCode);
        Assert.Equal(0, await factory.CountDirectConversationsAsync(a.UserId, b.UserId, ConversationScope.Personal, null));
    }

    private async Task<P0PersonalAccount> CreateP0PersonalAccountAsync(HttpClient client, HttpClient? owner, int birthYear = 1990)
    {
        var authentication = await AuthenticateAsync(client);
        var username = $"p0.{Guid.NewGuid():N}"[..20];
        using var profile = await client.PatchAsJsonAsync("/api/v1/me/profile", new CompleteProfileCommand("کاربر", "شخصی", "آزمون شخصی", username));
        profile.EnsureSuccessStatusCode();
        if (owner is not null)
        {
            var route = $"/api/admin/v1/users/{authentication.User.Id}/communication-age";
            var state = (await owner.GetFromJsonAsync<CommunicationAgeResult>(route, PolicyJson))!;
            using var verification = await owner.PostAsJsonAsync(route + "/verifications", new VerifyCommunicationAgeCommand(new DateOnly(birthYear, 1, 1), state.Version, "شاهد کنترل‌شدهٔ پذیرش محلی"));
            verification.EnsureSuccessStatusCode();
        }
        return new P0PersonalAccount(authentication.User.Id, username, authentication);
    }

    private static async Task AssertP0DiscoverableAsync(HttpClient client, string username)
    {
        var page = (await client.GetFromJsonAsync<PeopleSearchPageResult>($"/api/v1/people/search?q={username}", PolicyJson))!;
        Assert.Equal(username, Assert.Single(page.Items).Username);
        using var response = await client.PostAsJsonAsync("/api/v1/people/resolve", new ResolvePersonCommand(username, null));
        response.EnsureSuccessStatusCode();
        var resolved = (await response.Content.ReadFromJsonAsync<PeopleResolveResult>(PolicyJson))!;
        Assert.True(resolved.IsDiscoverable);
        Assert.True(resolved.Profile!.CanStartDirect);
    }

    private sealed record P0PersonalAccount(Guid UserId, string Username, VerifyOtpResult Authentication);
}
