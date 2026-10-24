using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Platform;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    [Fact]
    public async Task PersonalAdultsReceiveFirstConversationReplyAndReconnectAfterLostSuccessfulPost()
    {
        // فقط یک dispatcher و Hub محلی فعال است؛ هر دو میزبان هویت تست یکسان دارند.
        _host.Dispose();
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIdentitySecurity>();
            services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.RemoveAll<IHostedService>();
        }));
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var first = _host.CreateClient();
        using var second = _host.CreateClient();
        var a = await CreateP0PersonalAccountAsync(first, owner);
        var b = await CreateP0PersonalAccountAsync(second, owner);
        await using var receiver = CreateP0HubConnection(b.Authentication.AccessToken);
        var firstReceived = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, notification =>
        {
            if (notification.Type == MessagingChangeType.MessageCreated) firstReceived.TrySetResult(notification);
        });
        await receiver.StartAsync();
        // گیرنده پیش از وجود گفتگو وصل شده است و هنوز SubscribeConversation نکرده است.
        using var create = await first.PostAsJsonAsync("/api/v1/conversations/direct", new StartDirectConversationCommand(b.UserId, ConversationScope.Personal), PolicyJson);
        create.EnsureSuccessStatusCode();
        var direct = (await create.Content.ReadFromJsonAsync<DirectConversationResult>(PolicyJson))!;
        var route = $"/api/v1/conversations/{direct.ConversationId}/messages";
        var command = new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پیام ثبت‌شده با پاسخ گم‌شده" };
        using (var uncertain = new HttpClient(new LoseSuccessfulMessageResponseHandler(_host.Server.CreateHandler())) { BaseAddress = _host.Server.BaseAddress })
        {
            uncertain.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", a.Authentication.AccessToken);
            await Assert.ThrowsAsync<HttpRequestException>(() => uncertain.PostAsJsonAsync(route, command, PolicyJson));
        }
        var notification = await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var sent = await SendP0MessageAsync(first, route, command);
        Assert.Equal(sent.MessageId, notification.ResourceId);
        Assert.Equal(direct.ConversationId, notification.ConversationId);
        Assert.Contains((await second.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", PolicyJson))!, c => c.ConversationId == direct.ConversationId);
        var retries = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => SendP0MessageAsync(first, route, command)));
        Assert.All(retries, retry => Assert.Equal(sent, retry));
        using var conflictingRetry = await first.PostAsJsonAsync(route,
            new SendMessageCommand { ClientMessageId = command.ClientMessageId, Text = "محتوای متفاوت" }, PolicyJson);
        Assert.Equal(HttpStatusCode.Conflict, conflictingRetry.StatusCode);
        Assert.Single(await factory.GetMessagesAsync(direct.ConversationId));
        Assert.Single((await GetP0ChangesAsync(second, direct.ConversationId)).Changes, c => c.Type == MessagingChangeType.MessageCreated);
        _ = await factory.DrainNotificationOutboxAsync();
        Assert.Equal(1, await factory.CountNotificationsAsync(sent.MessageId, b.UserId));

        await using var sender = CreateP0HubConnection(a.Authentication.AccessToken);
        var replyReceived = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        sender.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, n =>
        {
            if (n.Type == MessagingChangeType.MessageCreated && n.ResourceId != sent.MessageId) replyReceived.TrySetResult(n);
        });
        await sender.StartAsync();
        await receiver.InvokeAsync("SubscribeConversation", direct.ConversationId);
        await receiver.InvokeAsync("UnsubscribeConversation", direct.ConversationId);
        var reply = await SendP0MessageAsync(second, route, new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پاسخ مخاطب" });
        Assert.Equal(reply.MessageId, (await replyReceived.Task.WaitAsync(TimeSpan.FromSeconds(15))).ResourceId);
        Assert.Equal(sent.Sequence + 1, reply.Sequence);
        var processed = await GetP0ChangesAsync(second, direct.ConversationId);
        await receiver.StopAsync();
        var missed = await SendP0MessageAsync(first, route, new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پیام هنگام قطع اتصال" });
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (await factory.CountPendingRealtimeOutboxAsync(direct.ConversationId) != 0 && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        Assert.Equal(0, await factory.CountPendingRealtimeOutboxAsync(direct.ConversationId));
        await receiver.StartAsync();
        var recovered = await GetP0ChangesAsync(second, direct.ConversationId, processed.NextCursor);
        Assert.Equal(missed.MessageId, Assert.Single(recovered.Changes).ResourceId);
        var replay = await GetP0ChangesAsync(second, direct.ConversationId, processed.NextCursor);
        Assert.Equal(Assert.Single(recovered.Changes).ChangeId, Assert.Single(replay.Changes).ChangeId);
        var history = (await second.GetFromJsonAsync<ConversationMessagePageResult>(route, PolicyJson))!;
        Assert.Equal(new[] { sent.MessageId, reply.MessageId, missed.MessageId }, history.Messages.Select(m => m.MessageId));
        Assert.Equal(3, history.Messages.Select(m => m.ClientMessageId).Distinct().Count());
        using var logout = await second.PostAsync("/api/v1/auth/logout", null);
        logout.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync(route)).StatusCode);
        await Assert.ThrowsAnyAsync<Exception>(() => receiver.InvokeAsync("SubscribeConversation", direct.ConversationId));
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await db.Memberships.AnyAsync(m => m.UserId == a.UserId || m.UserId == b.UserId));
    }

    [Fact]
    public async Task Discovery429ExposesRetryAfterToBrowserWithoutChangingOtpLimits()
    {
        _host.Dispose();
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.PostConfigure<RateLimiterOptions>(options => options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/api/v1/people")
                    ? RateLimitPartition.GetFixedWindowLimiter("p0-discovery", _ => new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter("other")));
        }));
        using var client = _host.CreateClient();
        _ = await CreateP0PersonalAccountAsync(client, null);
        client.DefaultRequestHeaders.Add("Origin", "https://messenger-client.test");
        using var allowed = await client.GetAsync("/api/v1/people/search?q=notfound");
        allowed.EnsureSuccessStatusCode();
        using var rejected = await client.GetAsync("/api/v1/people/search?q=notfound");
        await ErrorAsync(rejected, HttpStatusCode.TooManyRequests, MessagingErrorCodes.DiscoveryRateLimited);
        Assert.NotNull(rejected.Headers.RetryAfter?.Delta);
        Assert.InRange(rejected.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 60);
        Assert.Contains("Retry-After", string.Join(',', rejected.Headers.GetValues("Access-Control-Expose-Headers")), StringComparison.OrdinalIgnoreCase);
    }

    private HubConnection CreateP0HubConnection(string token) => new HubConnectionBuilder()
        .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/messaging"), options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
        })
        .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
        .Build();

    private static async Task<MessageReceiptResult> SendP0MessageAsync(HttpClient client, string route, SendMessageCommand command)
    {
        using var response = await client.PostAsJsonAsync(route, command, PolicyJson);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MessageReceiptResult>(PolicyJson))!;
    }

    private static async Task<ConversationChangePageResult> GetP0ChangesAsync(HttpClient client, Guid conversationId, string? cursor = null)
        => (await client.GetFromJsonAsync<ConversationChangePageResult>($"/api/v1/conversations/{conversationId}/changes" +
            (cursor is null ? string.Empty : $"?afterCursor={Uri.EscapeDataString(cursor)}"), PolicyJson))!;

    private sealed class LoseSuccessfulMessageResponseHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Post && response.IsSuccessStatusCode)
            {
                response.Dispose();
                throw new HttpRequestException("قطع آزمایشی پاسخ پس از موفقیت سرور");
            }
            return response;
        }
    }
}
