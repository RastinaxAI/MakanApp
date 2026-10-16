using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(CommunicationAgeCategory.Minor)]
    [InlineData(CommunicationAgeCategory.Unknown)]
    public async Task P2ConversationInfoProtectedContentUsesExistingSafetyAndScopedCase(CommunicationAgeCategory age)
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        await factory.SetCommunicationAgeCategoryAsync(setup.Target.UserId, age);
        var command = new UpdateConversationInfoCommand("عنوان تازه", "توضیح تازه", setup.Conversation.Version);
        try
        {
            factory.ContentSafetyProvider.ConfigureUnavailable();
            var unavailable = await setup.OwnerClient.PatchAsJsonAsync($"/api/v1/conversations/{id}/info", command, JsonOptions);
            Assert.Equal(MessagingErrorCodes.MessageSafetyUnavailable, await ReadProblemCodeAsync(unavailable));
            factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Blocked, policyCodes: ["p2-info-policy"]);
            var blocked = await setup.OwnerClient.PatchAsJsonAsync($"/api/v1/conversations/{id}/info", command, JsonOptions);
            Assert.Equal(MessagingErrorCodes.MessageSafetyBlocked, await ReadProblemCodeAsync(blocked));
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var unchanged = await db.Conversations.AsNoTracking().SingleAsync(c => c.Id == id);
            Assert.Equal(setup.Conversation.Title, unchanged.Title); Assert.Equal(setup.Conversation.Version, Convert.ToBase64String(unchanged.RowVersion));
            var incident = await db.SafetyIncidents.SingleAsync(i => i.ConversationId == id);
            Assert.Contains("توضیح تازه", incident.ContentSnapshot!); Assert.True(await db.ModerationCases.AnyAsync(c => c.SafetyIncidentId == incident.Id));
            Assert.False(await db.Messages.AnyAsync(m => m.ConversationId == id));
            factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
            // تلاش مسدودشدهٔ یکسان idempotent می‌ماند؛ متن متفاوت، تلاش مستقل است.
            var accepted = await setup.OwnerClient.PatchAsJsonAsync($"/api/v1/conversations/{id}/info", command with { Title = "عنوان مجاز" }, JsonOptions);
            accepted.EnsureSuccessStatusCode(); Assert.NotEqual(command.ExpectedVersion, (await accepted.Content.ReadFromJsonAsync<ConversationInfoResult>(JsonOptions))!.Version);
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }

    [Fact]
    public async Task P2ConversationInfoPermissionRevokedDuringSafetyCannotCommitMetadata()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        await PromoteP2AdminAsync(setup.OwnerClient, id, setup.Target.UserId, ConversationAdminPermission.ChangeInfo);
        await factory.SetCommunicationAgeCategoryAsync(setup.Target.UserId, CommunicationAgeCategory.Minor);
        var current = (await setup.TargetClient.GetFromJsonAsync<ManagedConversationResult>($"/api/v1/conversations/{id}", JsonOptions))!;
        var invoked = false;
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed, beforeReturn: async () =>
        { invoked = true; await PromoteP2AdminAsync(setup.OwnerClient, id, setup.Target.UserId, ConversationAdminPermission.None); });
        try
        {
            var denied = await setup.TargetClient.PatchAsJsonAsync($"/api/v1/conversations/{id}/info", new UpdateConversationInfoCommand("نباید ثبت شود", null, current.Version), JsonOptions);
            Assert.True(invoked); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.Equal(setup.Conversation.Title, (await db.Conversations.AsNoTracking().SingleAsync(c => c.Id == id)).Title);
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }

    [Fact]
    public async Task P2TopicExactRetryDoesNotNeedAvailableSafetyProviderOrCreateSecondTopic()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        await factory.SetCommunicationAgeCategoryAsync(setup.Target.UserId, CommunicationAgeCategory.Minor);
        var command = new CreateConversationTopicCommand(Guid.NewGuid(), "موضوع ثبت‌شده");
        var created = await CreateP2TopicAsync(setup.OwnerClient, id, command); factory.ContentSafetyProvider.ConfigureUnavailable();
        try
        {
            var retry = await CreateP2TopicAsync(setup.OwnerClient, id, command);
            Assert.True(retry.AlreadyExisted); Assert.Equal(created.Version, retry.Version); Assert.Empty(factory.ContentSafetyProvider.Requests);
            Assert.Single((await setup.OwnerClient.GetFromJsonAsync<ConversationTopicPageResult>($"/api/v1/conversations/{id}/topics", JsonOptions))!.Items);
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }
}
