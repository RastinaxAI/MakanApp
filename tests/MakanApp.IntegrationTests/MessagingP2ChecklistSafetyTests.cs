using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Outbox;
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
    [Theory]
    [InlineData(CommunicationAgeCategory.Minor)] [InlineData(CommunicationAgeCategory.Unknown)]
    public async Task P2ChecklistProtectedCreationAndDefinitionSafetyInspectAllTextAndFailClosed(CommunicationAgeCategory age)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, age);
        await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId); var command = P2Checklist();
        try
        {
            factory.ContentSafetyProvider.ConfigureUnavailable();
            var response = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/checklists", command, JsonOptions);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
            factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Blocked);
            response = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/checklists", command, JsonOptions);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var reviewed = Assert.Single(factory.ContentSafetyProvider.Requests); Assert.Contains(command.Title, reviewed.Text); Assert.All(command.Items, i => Assert.Contains(i.Text, reviewed.Text));
            factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed);
            var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, command with { ClientMessageId = Guid.NewGuid() });
            var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
            factory.ContentSafetyProvider.ConfigureUnavailable();
            response = await UpdateP2ChecklistResponseAsync(setup.FirstClient, checklist, P2ChecklistUpdate(checklist) with { Title = "نباید منتشر شود" });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(checklist.Version, (await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId)).Version);
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.Single(await db.ConversationChecklists.Where(c => c.ConversationId == setup.ConversationId).ToArrayAsync());
            Assert.True(await db.ModerationCases.AnyAsync(c => c.SafetyIncidentId != null && db.SafetyIncidents.Any(i => i.Id == c.SafetyIncidentId && i.ConversationId == setup.ConversationId)));
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }

    [Fact]
    public async Task P2ChecklistRealInternalSafetyRejectsUnsafeItemInsteadOfOnlyCheckingTitle()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, CommunicationAgeCategory.Minor);
        await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>(); services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.RemoveAll<IContentSafetyProvider>(); services.AddSingleton<IContentSafetyProvider, InternalMakanContentSafetyProvider>();
        }));
        using var client = host.CreateClient(); UseBearerToken(client, setup.First.AccessToken);
        using var response = await client.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/checklists", P2Checklist() with { Items = [new(Guid.NewGuid(), "می کشمت")] }, JsonOptions);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode); Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
        await using var scope = factory.Services.CreateAsyncScope(); Assert.Contains("می کشمت", (await scope.ServiceProvider.GetRequiredService<MakanDbContext>().SafetyIncidents.SingleAsync(i => i.ConversationId == setup.ConversationId)).ContentSnapshot);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task P2ChecklistCreationRevalidatesSessionAndBlockAfterSafety(bool revokeSession)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); await factory.SetCommunicationAgeCategoryAsync(setup.Second.UserId, CommunicationAgeCategory.Minor);
        await factory.AllowPersonalGroupInviteAsync(setup.First.UserId, setup.Second.UserId);
        try
        {
            factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed, beforeReturn: async () =>
            {
                if (revokeSession) await factory.RevokeSessionByTokenAsync(setup.First.AccessToken);
                else (await setup.SecondClient.PostAsync($"/api/v1/messaging/blocks/{setup.First.UserId}", null)).EnsureSuccessStatusCode();
            });
            using var response = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/checklists", P2Checklist(), JsonOptions);
            Assert.Equal(revokeSession ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
            await using var scope = factory.Services.CreateAsyncScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().ConversationChecklists.AnyAsync(c => c.ConversationId == setup.ConversationId));
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }

    [Fact]
    public async Task P2ChecklistAdminPermissionRevokedDuringSafetyCannotUseTrackedPreflightAuthority()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var receipt = await CreateP2ChecklistAsync(setup.OwnerClient, id); var checklist = await ReadP2ChecklistAsync(setup.TargetClient, id, receipt.MessageId);
        await PromoteP2AdminAsync(setup.OwnerClient, id, setup.Target.UserId, ConversationAdminPermission.DeleteMessages);
        await factory.SetCommunicationAgeCategoryAsync(setup.Target.UserId, CommunicationAgeCategory.Minor);
        try
        {
            factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed, beforeReturn: () => PromoteP2AdminAsync(setup.OwnerClient, id, setup.Target.UserId, ConversationAdminPermission.PinMessages));
            using var response = await UpdateP2ChecklistResponseAsync(setup.TargetClient, checklist, P2ChecklistUpdate(checklist) with { Title = "مجوز منقضی" });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(checklist.Title, (await ReadP2ChecklistAsync(setup.OwnerClient, id, receipt.MessageId)).Title);
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }

    [Fact]
    public async Task P2ChecklistReportSnapshotContainsOnlyDefinitionAndExistingCaseScope()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId);
        var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
        await SetP2ChecklistAsync(setup.FirstClient, checklist, checklist.Items.First().ItemId, true);
        var command = new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.Other, "گزارش همان محتوا");
        using var response = await setup.SecondClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{receipt.MessageId}/reports", command, JsonOptions); response.EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var report = await db.AbuseReports.SingleAsync(r => r.ClientReportId == command.ClientReportId);
        Assert.Equal(MessageKind.Checklist, report.ReportedMessageKind); Assert.Contains(checklist.Title, report.ReportedContentSnapshot);
        Assert.All(checklist.Items, i => { Assert.Contains(i.Text, report.ReportedContentSnapshot); Assert.DoesNotContain(i.ItemId.ToString(), report.ReportedContentSnapshot!); });
        Assert.DoesNotContain("isChecked", report.ReportedContentSnapshot!, StringComparison.OrdinalIgnoreCase);
        Assert.True(await db.ModerationCases.AnyAsync(c => c.AbuseReportId == report.Id));
        using var ordinaryReader = factory.CreateClient(); await CreateMessagingUserAsync(ordinaryReader);
        var caseId = await db.ModerationCases.Where(c => c.AbuseReportId == report.Id).Select(c => c.Id).SingleAsync();
        Assert.False((await ordinaryReader.GetAsync($"/api/admin/v1/moderation/cases/{caseId}")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task P2ChecklistOutboxFailureRollsBackMessageDefinitionAndItemsBeforeSafeRetry()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var command = P2Checklist();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>(); services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.AddSingleton<P2ScheduledOutboxControl>(); services.AddScoped<EfOutbox>(); services.RemoveAll<IOutbox>();
            services.AddScoped<IOutbox>(p => new P2FailingScheduledOutbox(p.GetRequiredService<EfOutbox>(), p.GetRequiredService<P2ScheduledOutboxControl>()));
        }));
        using var client = host.CreateClient(); UseBearerToken(client, setup.First.AccessToken);
        using var failure = await client.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/checklists", command, JsonOptions); Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
        await using (var scope = factory.Services.CreateAsyncScope()) Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().ConversationChecklists.AnyAsync(c => c.ConversationId == setup.ConversationId));
        var success = await CreateP2ChecklistAsync(client, setup.ConversationId, command); Assert.Equal(success, await CreateP2ChecklistAsync(client, setup.ConversationId, command));
        await factory.DrainNotificationOutboxAsync(); Assert.Equal(1, await factory.CountNotificationsAsync(success.MessageId, setup.Second.UserId));
    }
}
