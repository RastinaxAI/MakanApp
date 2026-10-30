using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Storage;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(CommunicationAgeCategory.Minor, "image/png", MessageKind.Image)]
    [InlineData(CommunicationAgeCategory.Unknown, "application/pdf", MessageKind.File)]
    public async Task Be01PendingReviewIsAuditableAndNeverCreatesDeliveryBindingOrDuplicateUsage(
        CommunicationAgeCategory age, string contentType, MessageKind kind)
    {
        using var setup = await CreateBe01SetupAsync(age);
        var file = await UploadFileAsync(setup.SenderClient, contentType,
            contentType == "image/png" ? "be01-private-file.png" : "be01-private-file.pdf");
        Assert.Equal(FileAssetStatus.Ready, file.Status);
        var command = new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = kind,
            Text = "BE01-private-caption", AttachmentIds = [file.Id] };
        var checkpoint = await AccountChangesAsync(setup.RecipientClient);
        var usageBefore = await Be01UsageAsync(setup.Sender.UserId);
        var logControl = setup.Host.Services.GetRequiredService<SensitiveLogTestControl>();
        logControl.Clear();
        var firstCorrelation = "be01-first-" + Guid.NewGuid().ToString("N");
        using var first = await Be01PostAsync(setup.RealSenderClient, setup.ConversationId, command, firstCorrelation);
        var reference = await AssertBe01FailureAsync(first, "PendingReview", MessagingErrorCodes.MessageSafetyReviewRequired);
        Assert.Equal(firstCorrelation, first.Headers.GetValues("X-Correlation-ID").Single());
        var retryCorrelation = "be01-retry-" + Guid.NewGuid().ToString("N");
        using var retry = await Be01PostAsync(setup.RealSenderClient, setup.ConversationId, command, retryCorrelation);
        Assert.Equal(reference, await AssertBe01FailureAsync(retry, "PendingReview", MessagingErrorCodes.MessageSafetyReviewRequired));

        using var changed = await setup.RealSenderClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages",
            new SendMessageCommand { ClientMessageId = command.ClientMessageId, Kind = kind,
                Text = "BE01-different-payload", AttachmentIds = [file.Id] }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageIdempotencyConflict, await ReadProblemCodeAsync(changed));

        using var outsiderClient = factory.CreateClient();
        var outsider = await CreateMessagingUserAsync(outsiderClient);
        using var denied = await outsiderClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages", command, JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.DoesNotContain(reference.ToString(), await denied.Content.ReadAsStringAsync());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var incident = await db.SafetyIncidents.SingleAsync(x => x.Id == reference);
            Assert.Equal(ContentSafetyDecision.ReviewRequired, incident.Decision);
            Assert.Contains(InternalMakanContentSafetyProvider.PolicyVersion + ":Other.UnsupportedMedia", incident.PolicyCodes);
            Assert.True(Guid.TryParseExact(incident.ProviderReference, "N", out _));
            var moderation = await db.ModerationCases.SingleAsync(x => x.SafetyIncidentId == reference);
            Assert.Equal(ModerationQueueScope.PlatformSafety, moderation.QueueScope);
            Assert.Equal(1, await db.ModerationEvidenceAttachments.CountAsync(x => x.ModerationCaseId == moderation.Id && x.FileAssetId == file.Id));
            Assert.False(await db.MessageAttachments.AnyAsync(x => x.FileAssetId == file.Id));
            var asset = await db.FileAssets.SingleAsync(x => x.Id == file.Id);
            Assert.Equal(FileAssetStatus.Ready, asset.Status);
            Assert.NotNull(asset.RetainedAtUtc);

            // فقط بازسازی وضعیت بسته‌شده در Fixture؛ این کد مسیر API یا مجوز تأیید رسانه ایجاد نمی‌کند.
            var now = factory.Clock.GetUtcNow().UtcDateTime;
            moderation.StartReview(outsider.UserId, now);
            moderation.Resolve(outsider.UserId, "بستن پرونده، مجوز ارسال رسانه نیست.", now);
            incident.Resolve(now);
            await db.SaveChangesAsync();
        }
        using var afterClose = await Be01PostAsync(setup.RealSenderClient, setup.ConversationId, command, "be01-closed-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(reference, await AssertBe01FailureAsync(afterClose, "PendingReview", MessagingErrorCodes.MessageSafetyReviewRequired));
        Assert.Equal(usageBefore, await Be01UsageAsync(setup.Sender.UserId));
        Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.Equal(1, await db.SafetyIncidents.CountAsync(x => x.ConversationId == setup.ConversationId));
            Assert.Equal(1, await db.ModerationCases.CountAsync(x => x.SafetyIncidentId == reference));
            Assert.False(await db.MessageAttachments.AnyAsync(x => x.FileAssetId == file.Id));
            Assert.False(await db.MessagingChangeEvents.AnyAsync(x => x.ConversationId == setup.ConversationId && x.ChangeType == MessagingChangeType.MessageCreated));
        }
        using var metadata = await setup.RecipientClient.GetAsync($"/api/v1/files/{file.Id}");
        using var download = await setup.RecipientClient.GetAsync($"/api/v1/files/{file.Id}/content");
        Assert.Equal(HttpStatusCode.NotFound, metadata.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        var changes = await DrainAccountAsync(setup.RecipientClient, checkpoint.NextCursor);
        Assert.DoesNotContain(changes.Values, x => x.Conversation?.Type == MessagingChangeType.MessageCreated);
        var summary = (await setup.RecipientClient.GetFromJsonAsync<ConversationSummaryResult>(
            $"/api/v1/conversations/{setup.ConversationId}/summary", JsonOptions))!;
        Assert.Equal(0, summary.UnreadCount);
        Assert.Null(summary.LastMessagePreview);

        var logs = logControl.Entries().Where(x => x.Properties.TryGetValue("SafetyReference", out var value) && Equals(value, reference)).ToArray();
        Assert.Equal(3, logs.Length);
        Assert.Contains(logs, x => Equals(x.Properties["Replayed"], false));
        Assert.Equal(2, logs.Count(x => Equals(x.Properties["Replayed"], true)));
        Assert.All(logs, entry =>
        {
            Assert.Equal("UnsupportedMedia", entry.Properties["SafetyReason"]);
            Assert.Equal("NotApplied", entry.Properties["OperationState"]);
            Assert.False(Assert.IsType<bool>(entry.Properties["NewMessageCreated"]));
            Assert.False(Assert.IsType<bool>(entry.Properties["NewAttachmentsBound"]));
            Assert.NotNull(entry.Properties["TraceId"]);
            Assert.Null(entry.ExceptionText);
            var values = string.Join(' ', entry.Properties.Values);
            Assert.DoesNotContain("BE01-private-caption", values);
            Assert.DoesNotContain("be01-private-file", values);
            Assert.DoesNotContain(setup.Sender.PhoneNumber, values);
            Assert.DoesNotContain(setup.Sender.AccessToken, values);
            Assert.DoesNotContain(InternalMakanContentSafetyProvider.PolicyVersion, values);
        });
        var scopes = logs.SelectMany(x => x.Scopes).OfType<IEnumerable<KeyValuePair<string, object?>>>().SelectMany(x => x).ToArray();
        Assert.Contains(scopes, x => x.Key == "CorrelationId" && Equals(x.Value, firstCorrelation));
        Assert.Contains(scopes, x => x.Key == "CorrelationId" && Equals(x.Value, retryCorrelation));
    }

    [Fact]
    public async Task Be01ConcurrentReviewRetriesShareOneIncidentAndNeverSendOrConsumeTwice()
    {
        using var setup = await CreateBe01SetupAsync(CommunicationAgeCategory.Minor);
        var file = await UploadFileAsync(setup.SenderClient, "image/png", "be01-concurrent.png");
        var before = await Be01UsageAsync(setup.Sender.UserId);
        var command = new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, AttachmentIds = [file.Id] };
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => setup.RealSenderClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages", command, JsonOptions)));
        try
        {
            var references = new List<Guid>();
            foreach (var response in responses)
                references.Add(await AssertBe01FailureAsync(response, "PendingReview", MessagingErrorCodes.MessageSafetyReviewRequired));
            Assert.Single(references.Distinct());
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
            Assert.Equal(before, await Be01UsageAsync(setup.Sender.UserId));
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.Equal(1, await db.SafetyIncidents.CountAsync(x => x.ConversationId == setup.ConversationId));
            var moderation = await db.ModerationCases.SingleAsync(x => x.SafetyIncidentId == references[0]);
            Assert.Equal(1, await db.ModerationEvidenceAttachments.CountAsync(x => x.ModerationCaseId == moderation.Id && x.FileAssetId == file.Id));
            Assert.False(await db.MessageAttachments.AnyAsync(x => x.FileAssetId == file.Id));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Be01RejectedTextAndAllowedTextHaveDistinctActualOutcomesWithoutReviewRelease()
    {
        using var setup = await CreateBe01SetupAsync(CommunicationAgeCategory.Minor);
        using var blocked = await SendMessageAsync(setup.RealSenderClient, setup.ConversationId, Guid.NewGuid(), "i will kill you");
        _ = await AssertBe01FailureAsync(blocked, "Rejected", MessagingErrorCodes.MessageSafetyBlocked);
        var allowed = await SendMessageAndReadAsync(setup.RealSenderClient, setup.ConversationId, Guid.NewGuid(), "سلام، تکلیف آماده است.");
        Assert.Equal(allowed.MessageId, Assert.Single(await factory.GetMessagesAsync(setup.ConversationId)).Id);
    }

    [Fact]
    public async Task Be01OpenApiExplainsPendingReviewAndRejectedWithoutPromisingHumanApproval()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var response = document.RootElement.GetProperty("paths").GetProperty("/api/v1/conversations/{conversationId}/messages")
            .GetProperty("post").GetProperty("responses").GetProperty("422");
        var properties = response.GetProperty("content").GetProperty("application/problem+json").GetProperty("schema").GetProperty("properties");
        Assert.Equal(new[] { "PendingReview", "Rejected" }, properties.GetProperty("safetyState").GetProperty("enum").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray());
        foreach (var name in new[] { "newMessageCreated", "newAttachmentsBound", "recipientDeliveryGranted", "recipientDownloadGranted", "retryable" })
            Assert.False(properties.GetProperty(name).GetProperty("enum")[0].GetBoolean());
        Assert.Contains("PendingReview", response.GetProperty("description").GetString());
    }

    private async Task<Be01Setup> CreateBe01SetupAsync(CommunicationAgeCategory age)
    {
        var senderClient = factory.CreateClient(); var recipientClient = factory.CreateClient();
        var sender = await CreateMessagingUserAsync(senderClient); var recipient = await CreateMessagingUserAsync(recipientClient);
        await factory.SetCommunicationAgeCategoryAsync(recipient.UserId, age);
        await factory.AllowPersonalGroupInviteAsync(sender.UserId, recipient.UserId);
        var conversation = await StartDirectAndReadAsync(senderClient, recipient.UserId, ConversationScope.Personal);
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IContentSafetyProvider>(); services.AddSingleton<IContentSafetyProvider, InternalMakanContentSafetyProvider>();
            services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.RemoveAll<IHostedService>();
        }));
        var realClient = host.CreateClient(); UseBearerToken(realClient, sender.AccessToken);
        return new(senderClient, recipientClient, sender, recipient, conversation.ConversationId, host, realClient);
    }

    private async Task<(int Ledger, int Reservations)> Be01UsageAsync(Guid owner)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return (await db.UsageLedgerEntries.CountAsync(x => x.OwnerId == owner), await db.UsageReservations.CountAsync(x => x.OwnerId == owner));
    }

    private static Task<HttpResponseMessage> Be01PostAsync(HttpClient client, Guid conversationId, SendMessageCommand command, string correlation)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/conversations/{conversationId}/messages")
            { Content = JsonContent.Create(command, options: JsonOptions) };
        request.Headers.Add("X-Correlation-ID", correlation);
        return SendAsync();
        async Task<HttpResponseMessage> SendAsync() { using (request) return await client.SendAsync(request); }
    }

    private static async Task<Guid> AssertBe01FailureAsync(HttpResponseMessage response, string state, string code)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json); var root = document.RootElement;
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal(state, root.GetProperty("safetyState").GetString());
        Assert.Equal("NotApplied", root.GetProperty("operationState").GetString());
        Assert.Equal("KEEP_DRAFT_NO_AUTO_RETRY", root.GetProperty("requiredAction").GetString());
        foreach (var name in new[] { "newMessageCreated", "newAttachmentsBound", "recipientDeliveryGranted", "recipientDownloadGranted", "retryable" })
            Assert.False(root.GetProperty(name).GetBoolean());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("correlationId").GetString()));
        Assert.False(string.IsNullOrEmpty(root.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("UnsupportedMedia", json);
        Assert.DoesNotContain(InternalMakanContentSafetyProvider.PolicyVersion, json);
        Assert.DoesNotContain("BE01-private-caption", json);
        return root.GetProperty("safetyReference").GetGuid();
    }

    private sealed record Be01Setup(HttpClient SenderClient, HttpClient RecipientClient, MessagingUser Sender,
        MessagingUser Recipient, Guid ConversationId, WebApplicationFactory<Program> Host, HttpClient RealSenderClient) : IDisposable
    {
        public void Dispose() { RealSenderClient.Dispose(); Host.Dispose(); SenderClient.Dispose(); RecipientClient.Dispose(); }
    }
}
