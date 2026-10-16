using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2TopicsCreateRetryAndFilteredHistoryUseSameConversation()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true);
        var command = new CreateConversationTopicCommand(Guid.NewGuid(), "موضوع اول");
        var topic = await CreateP2TopicAsync(setup.OwnerClient, setup.Conversation.ConversationId, command);
        var retry = await CreateP2TopicAsync(setup.OwnerClient, setup.Conversation.ConversationId, command);
        Assert.True(retry.AlreadyExisted); Assert.Equal(topic.TopicId, retry.TopicId);
        var second = await CreateP2TopicAsync(setup.OwnerClient, setup.Conversation.ConversationId, new(Guid.NewGuid(), "موضوع دوم"));
        (await SendP2TopicAsync(setup.OwnerClient, topic, Guid.NewGuid(), "اول")).EnsureSuccessStatusCode();
        (await SendP2TopicAsync(setup.OwnerClient, second, Guid.NewGuid(), "دوم")).EnsureSuccessStatusCode();
        await SendMessageAndReadAsync(setup.OwnerClient, topic.ConversationId, Guid.NewGuid(), "بدون موضوع");
        var history = await setup.TargetClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{topic.ConversationId}/messages?topicId={topic.TopicId}&limit=1", JsonOptions);
        Assert.Equal("اول", Assert.Single(history!.Messages).Text); Assert.Equal(topic.TopicId, Assert.Single(history.Messages).TopicId); Assert.Null(history.NextBeforeSequence);
        Assert.Equal(3, (await factory.GetMessagesAsync(topic.ConversationId)).Count());
        var page = await setup.TargetClient.GetFromJsonAsync<ConversationTopicPageResult>($"/api/v1/conversations/{topic.ConversationId}/topics", JsonOptions);
        Assert.Equal(2, page!.Items.Count); Assert.False(page.CanManage);
    }
    [Fact]
    public async Task P2TopicsClosePreservesHistoryRejectsSendAndExplicitReopenWorks()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true);
        var topic = await CreateP2TopicAsync(setup.OwnerClient, setup.Conversation.ConversationId, new(Guid.NewGuid(), "موضوع"));
        (await SendP2TopicAsync(setup.TargetClient, topic, Guid.NewGuid(), "تاریخچه")).EnsureSuccessStatusCode();
        var closed = await UpdateP2TopicAsync(setup.OwnerClient, topic, ConversationTopicStatus.Closed);
        var denied = await SendP2TopicAsync(setup.TargetClient, closed, Guid.NewGuid(), "نباید ثبت شود");
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode); Assert.Equal(MessagingErrorCodes.TopicClosed, await ReadProblemCodeAsync(denied));
        Assert.Single((await setup.TargetClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{topic.ConversationId}/messages?topicId={topic.TopicId}", JsonOptions))!.Messages);
        var reopened = await UpdateP2TopicAsync(setup.OwnerClient, closed, ConversationTopicStatus.Open);
        (await SendP2TopicAsync(setup.TargetClient, reopened, Guid.NewGuid(), "پس از بازگشایی")).EnsureSuccessStatusCode();
        Assert.Equal(2, (await factory.GetMessagesAsync(topic.ConversationId)).Count());
    }
    [Fact]
    public async Task P2TopicsGranularPermissionAndRevocationApplyToEveryMutation()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.TargetClient.PostAsJsonAsync($"/api/v1/conversations/{id}/topics", new CreateConversationTopicCommand(Guid.NewGuid(), "بدون اختیار"), JsonOptions)).StatusCode);
        await PromoteP2AdminAsync(setup.OwnerClient, id, setup.Target.UserId, ConversationAdminPermission.ManageTopics);
        var topic = await CreateP2TopicAsync(setup.TargetClient, id, new(Guid.NewGuid(), "مجاز"));
        await PromoteP2AdminAsync(setup.OwnerClient, id, setup.Target.UserId, ConversationAdminPermission.ManageInvites);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.TargetClient.PutAsJsonAsync($"/api/v1/conversations/{id}/topics/{topic.TopicId}", new UpdateConversationTopicCommand(topic.Title, ConversationTopicStatus.Closed, topic.Version), JsonOptions)).StatusCode);
        Assert.False((await setup.TargetClient.GetFromJsonAsync<ConversationTopicPageResult>($"/api/v1/conversations/{id}/topics", JsonOptions))!.CanManage);
    }
    [Fact]
    public async Task P2TopicsStaleVersionCannotRewriteNewerState()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups");
        var topic = await CreateP2TopicAsync(setup.OwnerClient, setup.Conversation.ConversationId, new(Guid.NewGuid(), "موضوع"));
        using var secondDevice = factory.CreateClient(); await AuthenticateP1SessionAsync(secondDevice, setup.Owner.PhoneNumber, factory.Services);
        var updated = await UpdateP2TopicAsync(setup.OwnerClient, topic, ConversationTopicStatus.Closed);
        var stale = await secondDevice.PutAsJsonAsync($"/api/v1/conversations/{topic.ConversationId}/topics/{topic.TopicId}", new UpdateConversationTopicCommand("بازنویسی", ConversationTopicStatus.Open, topic.Version), JsonOptions);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(ConversationTopicStatus.Closed, Assert.Single((await setup.OwnerClient.GetFromJsonAsync<ConversationTopicPageResult>($"/api/v1/conversations/{topic.ConversationId}/topics", JsonOptions))!.Items).Status);
        Assert.NotEqual(topic.Version, updated.Version);
    }
    [Fact]
    public async Task P2TopicsCrossConversationReferenceFailsInApiAndSql()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups"); var first = setup.Conversation.ConversationId;
        var other = await CreateManagedAndReadAsync(setup.OwnerClient, "groups", PersonalCommand("گروه دوم"));
        var topic = await CreateP2TopicAsync(setup.OwnerClient, first, new(Guid.NewGuid(), "فقط گروه اول"));
        var wrong = topic with { ConversationId = other.ConversationId };
        Assert.Equal(HttpStatusCode.NotFound, (await SendP2TopicAsync(setup.OwnerClient, wrong, Guid.NewGuid(), "نباید ثبت شود")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var participant = await db.ConversationParticipants.SingleAsync(p => p.ConversationId == other.ConversationId && p.UserId == setup.Owner.UserId);
        db.Messages.Add(Message.Create(other.ConversationId, participant.Id, setup.Owner.UserId, Guid.NewGuid(), 1, MessageKind.Text, "نامرتبط", 0, null, null, 4000, DateTime.UtcNow, topic.TopicId));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sql = Assert.IsType<Microsoft.Data.SqlClient.SqlException>(error.InnerException); Assert.Equal(547, sql.Number); Assert.Contains("FK_Messages_ConversationTopics", sql.Message);
    }
    [Fact]
    public async Task P2TopicsConcurrentCloseAndSendHaveOneSerializedOutcome()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true);
        var topic = await CreateP2TopicAsync(setup.OwnerClient, setup.Conversation.ConversationId, new(Guid.NewGuid(), "رقابت"));
        var close = setup.OwnerClient.PutAsJsonAsync($"/api/v1/conversations/{topic.ConversationId}/topics/{topic.TopicId}", new UpdateConversationTopicCommand(topic.Title, ConversationTopicStatus.Closed, topic.Version), JsonOptions);
        var send = SendP2TopicAsync(setup.TargetClient, topic, Guid.NewGuid(), "هم‌زمان");
        await Task.WhenAll(close, send); var closeResponse = await close; var sendResponse = await send;
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        Assert.Contains(sendResponse.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        Assert.Equal(sendResponse.IsSuccessStatusCode ? 1 : 0, (await factory.GetMessagesAsync(topic.ConversationId)).Count());
        Assert.Equal(HttpStatusCode.Conflict, (await SendP2TopicAsync(setup.TargetClient, topic, Guid.NewGuid(), "پس از بسته شدن")).StatusCode);
    }
    [Fact]
    public async Task P2TopicsRemovedMemberCannotReadWriteOrReceiveFutureTopicDelta()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var topic = await CreateP2TopicAsync(setup.OwnerClient, id, new(Guid.NewGuid(), "موضوع"));
        (await setup.OwnerClient.DeleteAsync($"/api/v1/conversations/{id}/members/{setup.Target.UserId}")).EnsureSuccessStatusCode();
        await UpdateP2TopicAsync(setup.OwnerClient, topic, ConversationTopicStatus.Closed);
        foreach (var path in new[] { $"/api/v1/conversations/{id}/topics", $"/api/v1/conversations/{id}/messages?topicId={topic.TopicId}", $"/api/v1/conversations/{id}/changes?afterChangeSequence=0" })
            Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendP2TopicAsync(setup.TargetClient, topic, Guid.NewGuid(), "رد")).StatusCode);
    }
    private static async Task<ConversationTopicResult> CreateP2TopicAsync(HttpClient client, Guid conversationId, CreateConversationTopicCommand command)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/topics", command, JsonOptions); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationTopicResult>(JsonOptions))!;
    }
    private static async Task<ConversationTopicResult> UpdateP2TopicAsync(HttpClient client, ConversationTopicResult topic, ConversationTopicStatus status)
    {
        using var response = await client.PutAsJsonAsync($"/api/v1/conversations/{topic.ConversationId}/topics/{topic.TopicId}", new UpdateConversationTopicCommand(topic.Title, status, topic.Version), JsonOptions); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationTopicResult>(JsonOptions))!;
    }
    private static Task<HttpResponseMessage> SendP2TopicAsync(HttpClient client, ConversationTopicResult topic, Guid clientMessageId, string text) =>
        client.PostAsJsonAsync($"/api/v1/conversations/{topic.ConversationId}/messages", new SendMessageCommand { ClientMessageId = clientMessageId, Text = text, TopicId = topic.TopicId }, JsonOptions);
}
