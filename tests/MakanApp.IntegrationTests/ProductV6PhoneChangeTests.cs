using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    private static string NewPhone() => $"+98939{Interlocked.Increment(ref _phone):D7}";
    private Task<PhoneChangeResult> RequestPhone(TestActor actor, string phone, Guid? operation = null) =>
        PostAsync<PhoneChangeResult>(actor.Client, "/api/v1/me/phone-change/requests", new RequestPhoneChangeCommand(operation ?? Guid.NewGuid(), phone));
    private static string PhoneVerifyRoute(Guid request) => $"/api/v1/me/phone-change/requests/{request}/verify";

    [Fact]
    public async Task ProductV6PhoneChangePreservesIdentityAndCurrentSessionButRevokesOtherAccessAndRefresh()
    {
        var actor = await ActorAsync(); await CompleteContextActor(actor);
        var otherClient = factory.CreateClient(); _clients.Add(otherClient);
        factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(1));
        var challenge = await PostAsync<RequestOtpResult>(otherClient, "/api/v1/auth/otp/challenges", new RequestOtpCommand(actor.Phone));
        var other = await PostAsync<VerifyOtpResult>(otherClient, "/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, actor.Phone, factory.GetLatestOtpCode(actor.Phone)));
        otherClient.DefaultRequestHeaders.Authorization = new("Bearer", other.AccessToken);
        var before = await GetAsync<CurrentUserResult>(actor.Client, "/api/v1/me");
        var phone = NewPhone(); var operation = Guid.NewGuid(); var count = factory.OtpProvider.InvocationCount;
        PhoneChangeResult request;
        factory.OtpProvider.OverrideVerificationCode = "synthetic-private-code-" + Guid.NewGuid().ToString("N");
        try { request = await RequestPhone(actor, phone, operation); }
        finally { factory.OtpProvider.OverrideVerificationCode = null; }
        var code = factory.GetLatestOtpCode(phone);
        Assert.Equal("Ready", request.State);
        Assert.Equal(request, await RequestPhone(actor, phone, operation)); Assert.Equal(count + 1, factory.OtpProvider.InvocationCount);
        using var wrong = await actor.Client.PostAsJsonAsync(PhoneVerifyRoute(request.RequestId), new VerifyPhoneChangeCommand(code + "x"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var success = await PostAsync<PhoneChangeResult>(actor.Client, PhoneVerifyRoute(request.RequestId), new VerifyPhoneChangeCommand(code));
        Assert.Equal("Completed", success.State);
        Assert.Equal(success, await PostAsync<PhoneChangeResult>(actor.Client, PhoneVerifyRoute(request.RequestId), new VerifyPhoneChangeCommand(code)));
        var after = await GetAsync<CurrentUserResult>(actor.Client, "/api/v1/me");
        Assert.Equal(before.Id, after.Id); Assert.Equal(before.Username, after.Username); Assert.Equal(before.FirstName, after.FirstName); Assert.Equal(phone, after.PhoneNumber);
        using var otherDenied = await otherClient.GetAsync("/api/v1/me"); Assert.Equal(HttpStatusCode.Unauthorized, otherDenied.StatusCode);
        using var refreshDenied = await otherClient.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshSessionCommand(other.RefreshToken), Json);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshDenied.StatusCode);
        await WithDb(async db => {
            Assert.False(await db.UserCredentials.AnyAsync(c => c.NormalizedIdentifier == actor.Phone));
            Assert.Equal(actor.UserId, await db.UserCredentials.Where(c => c.NormalizedIdentifier == phone).Select(c => c.UserId).SingleAsync());
            Assert.False(await db.OtpChallenges.AnyAsync(c => c.NormalizedPhoneNumber == phone));
            Assert.Null((await db.UserSessions.AsNoTracking().SingleAsync(s => s.Id == actor.SessionId)).RevokedAtUtc);
            Assert.True(await db.RefreshTokens.AnyAsync(t => t.SessionId == actor.SessionId && t.RevokedAtUtc == null && t.UsedAtUtc == null));
            Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.ActorUserId == actor.UserId && a.Action == "identity.PhoneChanged"));
        });
        var logs = string.Join('\n', factory.SensitiveLogs.Snapshot());
        Assert.DoesNotContain(code, logs); Assert.DoesNotContain(phone, logs);
    }

    [Fact]
    public async Task ProductV6PhoneChangeConcurrentClaimsHaveOneWinnerAndNoPartialIdentityMutation()
    {
        var a = await ActorAsync(); var b = await ActorAsync(); var target = NewPhone();
        var first = await RequestPhone(a, target); var firstCode = factory.GetLatestOtpCode(target);
        factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(1));
        var second = await RequestPhone(b, target); var secondCode = factory.GetLatestOtpCode(target);
        var replies = await Task.WhenAll(a.Client.PostAsJsonAsync(PhoneVerifyRoute(first.RequestId), new VerifyPhoneChangeCommand(firstCode), Json),
            b.Client.PostAsJsonAsync(PhoneVerifyRoute(second.RequestId), new VerifyPhoneChangeCommand(secondCode), Json));
        try
        {
            Assert.Single(replies, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict);
            await WithDb(async db => {
                Assert.Equal(1, await db.UserCredentials.CountAsync(c => c.NormalizedIdentifier == target));
                Assert.Equal(1, await db.PhoneChangeRequests.CountAsync(r => r.TargetPhone == target && r.State == PhoneChangeState.Completed));
                var loser = replies[0].StatusCode == HttpStatusCode.Conflict ? a : b;
                Assert.Equal(loser.Phone, await db.UserCredentials.Where(c => c.UserId == loser.UserId).Select(c => c.NormalizedIdentifier).SingleAsync());
                Assert.Null((await db.UserSessions.SingleAsync(s => s.Id == loser.SessionId)).RevokedAtUtc);
            });
        }
        finally { foreach (var reply in replies) reply.Dispose(); }
    }

    [Fact]
    public async Task ProductV6PhoneChangeRequiresOwnSessionRealCodeAndRejectsExpiryAndTakenTarget()
    {
        var actor = await ActorAsync(); var outsider = await ActorAsync();
        using var taken = await actor.Client.PostAsJsonAsync("/api/v1/me/phone-change/requests", new RequestPhoneChangeCommand(Guid.NewGuid(), outsider.Phone), Json);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        var phone = NewPhone(); var request = await RequestPhone(actor, phone); var code = factory.GetLatestOtpCode(phone);
        using var foreign = await outsider.Client.PostAsJsonAsync(PhoneVerifyRoute(request.RequestId), new VerifyPhoneChangeCommand(code), Json);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        var other = await AnotherDeviceAsync(actor);
        using var crossSession = await other.Client.PostAsJsonAsync(PhoneVerifyRoute(request.RequestId), new VerifyPhoneChangeCommand(code), Json);
        Assert.Equal(HttpStatusCode.NotFound, crossSession.StatusCode);
        factory.Clock.SetUtcNow(request.ExpiresAtUtc.AddSeconds(1));
        using var expired = await actor.Client.PostAsJsonAsync(PhoneVerifyRoute(request.RequestId), new VerifyPhoneChangeCommand(code), Json);
        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);
        Assert.Equal(actor.Phone, (await GetAsync<CurrentUserResult>(actor.Client, "/api/v1/me")).PhoneNumber);
    }

    [Fact]
    public async Task ProductV6PhoneChangeHasAtomicDuplicateVerificationAndFinalAttemptLimit()
    {
        var actor = await ActorAsync(); var phone = NewPhone(); var request = await RequestPhone(actor, phone);
        var code = factory.GetLatestOtpCode(phone);
        var replies = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => actor.Client.PostAsJsonAsync(PhoneVerifyRoute(request.RequestId), new VerifyPhoneChangeCommand(code), Json)));
        foreach (var response in replies) { response.EnsureSuccessStatusCode(); response.Dispose(); }
        await WithDb(async db => Assert.Equal(1, await db.AdminAuditLogs.CountAsync(a => a.ActorUserId == actor.UserId && a.Action == "identity.PhoneChanged")));
        var second = await ActorAsync(); var target = NewPhone(); var pending = await RequestPhone(second, target);
        var correct = factory.GetLatestOtpCode(target);
        for (var i = 0; i < 5; i++)
        {
            using var failed = await second.Client.PostAsJsonAsync(PhoneVerifyRoute(pending.RequestId), new VerifyPhoneChangeCommand(correct + "x"), Json);
            Assert.Equal(i == 4 ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest, failed.StatusCode);
        }
        using var locked = await second.Client.PostAsJsonAsync(PhoneVerifyRoute(pending.RequestId), new VerifyPhoneChangeCommand(correct), Json);
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
    }

    [Fact]
    public async Task ProductV6PhoneChangeDoesNotResendOnUncertainProviderDelivery()
    {
        var actor = await ActorAsync(); var operation = new RequestPhoneChangeCommand(Guid.NewGuid(), NewPhone());
        var count = factory.OtpProvider.InvocationCount; factory.OtpProvider.Fail = true;
        try
        {
            using var failed = await actor.Client.PostAsJsonAsync("/api/v1/me/phone-change/requests", operation, Json);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        }
        finally { factory.OtpProvider.Fail = false; }
        using var retry = await actor.Client.PostAsJsonAsync("/api/v1/me/phone-change/requests", operation, Json);
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode); Assert.Equal(count + 1, factory.OtpProvider.InvocationCount);
        Assert.Equal(actor.Phone, (await GetAsync<CurrentUserResult>(actor.Client, "/api/v1/me")).PhoneNumber);
    }

    [Fact]
    public async Task ProductV6MockLoginCannotBypassPhoneChangeRealDeliveryAndVerification()
    {
        using var mock = factory.WithWebHostBuilder(builder => {
            builder.UseSetting("Otp:Enabled", "false"); builder.UseSetting("Otp:AllowMockAuthentication", "true");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Otp:Enabled"] = "false", ["Otp:AllowMockAuthentication"] = "true" }));
        });
        using var client = mock.CreateClient();
        var phone = NewPhone();
        var login = await PostAsync<VerifyOtpResult>(client, "/api/v1/auth/otp/verify", new VerifyOtpCommand(Guid.NewGuid(), phone, "arbitrary-code"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var target = NewPhone();
        var pending = await PostAsync<PhoneChangeResult>(client, "/api/v1/me/phone-change/requests", new RequestPhoneChangeCommand(Guid.NewGuid(), target));
        using var wrong = await client.PostAsJsonAsync(PhoneVerifyRoute(pending.RequestId), new VerifyPhoneChangeCommand("arbitrary-code"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var testProvider = mock.Services.GetService(typeof(OtpProviderTestControl)) as OtpProviderTestControl;
        Assert.NotNull(testProvider); Assert.Equal(1, testProvider.InvocationCount);
        var success = await PostAsync<PhoneChangeResult>(client, PhoneVerifyRoute(pending.RequestId), new VerifyPhoneChangeCommand(testProvider.GetLatestCode(target)));
        Assert.Equal("Completed", success.State);
    }
}
