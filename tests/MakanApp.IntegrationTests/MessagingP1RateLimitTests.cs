using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.RateLimiting;
using MakanApp.Api.Controllers;
using MakanApp.Api.Errors;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public void MessageWriteEndpointsHaveExplicitBoundedPolicies()
    {
        foreach (var pair in new[] { (nameof(ConversationsController.SendMessage), "messaging-send"),
            (nameof(ConversationsController.EditMessage), "messaging-send"), (nameof(ConversationsController.ForwardMessage), "messaging-forward"),
            (nameof(ConversationsController.AddReaction), "messaging-reaction"), (nameof(ConversationsController.RemoveReaction), "messaging-reaction") })
            Assert.Equal(pair.Item2, typeof(ConversationsController).GetMethod(pair.Item1)!.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Theory]
    [InlineData("send")]
    [InlineData("reaction")]
    [InlineData("forward")]
    [InlineData("upload")]
    [InlineData("search")]
    public async Task MessagingAndUploadThrottlingReturnsRetryAfterAndPreservesAuthenticationContract(string action)
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "p1-rate-source");
        var destination = await CreateManagedAndReadAsync(setup.FirstClient, "groups", PersonalCommand("p1-rate-destination"));
        var prefix = action == "upload" ? "/api/v1/files" : action == "search" ? "/api/v1/messages/search" : "/api/v1/conversations";
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.PostConfigure<RateLimiterOptions>(options => options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments(prefix)
                    ? RateLimitPartition.GetFixedWindowLimiter("p1-" + action, _ => new FixedWindowRateLimiterOptions
                        { PermitLimit = 1, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter("other")));
        }));
        using var client = host.CreateClient(); await AuthenticateP1SessionAsync(client, setup.First.PhoneNumber, host.Services);
        client.DefaultRequestHeaders.Add("Origin", "https://messenger-client.test");
        using var accepted = await ExecuteAsync(); accepted.EnsureSuccessStatusCode();
        using var rejected = await ExecuteAsync();
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter?.Delta);
        Assert.InRange(rejected.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 60);
        Assert.Contains("Retry-After", string.Join(',', rejected.Headers.GetValues("Access-Control-Expose-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(action == "search" ? MessagingErrorCodes.DiscoveryRateLimited : ApiErrorCodes.RateLimited, await ReadProblemCodeAsync(rejected));
        (await client.GetAsync("/api/v1/notifications/devices")).EnsureSuccessStatusCode();

        async Task<HttpResponseMessage> ExecuteAsync()
        {
            if (action == "search") return await client.GetAsync("/api/v1/messages/search?q=p1-rate-source");
            if (action == "upload")
            {
                using var form = new MultipartFormDataContent(); using var content = new ByteArrayContent("p1-rate-file"u8.ToArray());
                content.Headers.ContentType = new MediaTypeHeaderValue("text/plain"); form.Add(content, "File", "p1-rate.txt");
                return await client.PostAsync("/api/v1/files", form);
            }
            if (action == "reaction") return await client.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}/reactions",
                new AddReactionCommand(MessageReactionType.Like), JsonOptions);
            if (action == "forward") return await client.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}/forward",
                new ForwardMessageCommand(destination.ConversationId, Guid.NewGuid()), JsonOptions);
            return await client.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages",
                new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "p1-rate-send" }, JsonOptions);
        }
    }
}
