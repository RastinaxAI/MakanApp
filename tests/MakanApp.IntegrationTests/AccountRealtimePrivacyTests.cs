using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Realtime;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AccountRealtimePrivacyChangeReachesBothOwnerSessionsButNeverRevealsPrivateReasonToPeer()
    {
        using var peerClient = factory.CreateClient(); using var ownerClient = factory.CreateClient(); using var anotherDevice = factory.CreateClient();
        _ = await CreateMessagingUserAsync(peerClient); var owner = await CreateMessagingUserAsync(ownerClient);
        _ = await StartDirectAndReadAsync(peerClient, owner.UserId, ConversationScope.Personal);
        using var requested = await anotherDevice.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(owner.PhoneNumber), JsonOptions);
        var challenge = (await requested.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions))!;
        using var login = await anotherDevice.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challenge.ChallengeId, owner.PhoneNumber, factory.GetLatestOtpCode(owner.PhoneNumber)), JsonOptions);
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
        UseBearerToken(anotherDevice, auth.AccessToken);
        await FlushReplayOutboxAsync();
        await using var one = CreateHubConnection(owner.AccessToken); await using var two = CreateHubConnection(auth.AccessToken);
        var first = new TaskCompletionSource<RealtimeInvalidation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<RealtimeInvalidation>(TaskCreationOptions.RunContinuationsAsynchronously);
        one.On<RealtimeInvalidation>("ClientInvalidated", x => first.TrySetResult(x));
        two.On<RealtimeInvalidation>("ClientInvalidated", x => second.TrySetResult(x));
        await one.StartAsync(); await two.StartAsync();
        await UpdatePrivacyAsync(ownerClient, PrivacyAudience.Nobody, PrivacyAudience.Nobody, PrivacyAudience.Nobody, PrivacyAudience.Nobody, null);
        await FlushReplayOutboxAsync();
        var eventOne = await first.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var eventTwo = await second.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(eventOne.EventId, eventTwo.EventId);
        Assert.Equal("access", eventOne.Key); Assert.Null(eventOne.ResourceId);
        Assert.NotEqual(eventOne.ContextRevision, eventTwo.ContextRevision);
        await using var scope = factory.Services.CreateAsyncScope();
        var payload = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().OutboxMessages.Where(x => x.Id == eventOne.EventId).Select(x => x.Payload).SingleAsync();
        Assert.Equal(2, (await ReplayRecipientsAsync(eventOne.EventId, payload)).Count);
        using var denied = await StartDirectAsync(peerClient, owner.UserId, ConversationScope.Personal);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(MessagingErrorCodes.DirectRecipientNotAvailable, await ReadProblemCodeAsync(denied));
    }
}
