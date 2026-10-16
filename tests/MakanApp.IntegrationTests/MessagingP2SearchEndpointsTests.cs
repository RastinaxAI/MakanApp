using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2SearchReadyAttachmentAndLinkFiltersWorkWithoutQueryAndBeforePagination()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var plain = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "فقط متن");
        var link = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "HTTPS://example.test/درس");
        var image = await UploadFileAsync(setup.FirstClient, "image/png", "p2-filter.png");
        var media = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId, new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, Text = "http://example.test/image", AttachmentIds = [image.Id] });
        var withFile = await P2SearchAsync(setup.SecondClient, $"conversationId={setup.ConversationId}&hasAttachment=true");
        Assert.Equal(media.MessageId, Assert.Single(withFile.Items).MessageId); Assert.Equal(1, withFile.TotalCount);
        var withoutFile = await P2SearchAsync(setup.SecondClient, $"conversationId={setup.ConversationId}&hasAttachment=false"); Assert.Equal(2, withoutFile.TotalCount);
        var first = await P2SearchAsync(setup.SecondClient, $"conversationId={setup.ConversationId}&hasLinks=true&limit=1");
        var next = await P2SearchAsync(setup.SecondClient, $"conversationId={setup.ConversationId}&hasLinks=true&limit=1&cursor={Uri.EscapeDataString(first.NextCursor!)}");
        Assert.Equal(2, first.TotalCount); Assert.Equal(2, next.TotalCount); Assert.NotEqual(Assert.Single(first.Items).MessageId, Assert.Single(next.Items).MessageId); Assert.Null(next.NextCursor);
        Assert.Equal(plain.MessageId, Assert.Single((await P2SearchAsync(setup.SecondClient, $"conversationId={setup.ConversationId}&hasLinks=false")).Items).MessageId);
        Assert.Equal(link.MessageId, Assert.Single((await P2SearchAsync(setup.SecondClient, $"conversationId={setup.ConversationId}&hasAttachment=false&hasLinks=true")).Items).MessageId);
    }
    [Fact]
    public async Task P2SearchRetainedAttachmentCannotBeDeletedOrAdvertisedWithoutAccess()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var file = await UploadFileAsync(setup.FirstClient, "application/pdf", "p2-file.pdf");
        var sent = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId, new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.File, AttachmentIds = [file.Id] });
        Assert.Single((await P2SearchAsync(setup.SecondClient, $"conversationId={setup.ConversationId}&hasAttachment=true&kind=File")).Items);
        // فایلِ متصل به پیام retained است؛ برای تست فیلتر نباید constraint تاریخی را دور زد.
        var rejected = await Assert.ThrowsAsync<DbUpdateException>(() => factory.SetFileAssetStatusAsync(file.Id, FileAssetStatus.Deleted));
        Assert.Equal(547, Assert.IsType<SqlException>(rejected.InnerException).Number);
        Assert.Equal(sent.MessageId, Assert.Single((await P2SearchAsync(setup.SecondClient, $"conversationId={setup.ConversationId}&hasAttachment=true")).Items).MessageId);
        using var stranger = factory.CreateClient(); await CreateMessagingUserAsync(stranger);
        Assert.Equal(0, (await P2SearchAsync(stranger, $"conversationId={setup.ConversationId}&hasAttachment=true")).TotalCount);
    }
    [Fact]
    public async Task P2SearchFiltersCannotRestoreDeleteForMeClearOrInaccessibleCount()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var stranger = factory.CreateClient(); await CreateMessagingUserAsync(stranger);
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "https://example.test/private");
        var inaccessible = await P2SearchAsync(stranger, $"conversationId={setup.ConversationId}&hasLinks=true"); Assert.Equal(0, inaccessible.TotalCount); Assert.Null(inaccessible.NextCursor);
        (await setup.SecondClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}/for-me")).EnsureSuccessStatusCode();
        Assert.Equal(0, (await P2SearchAsync(setup.SecondClient, $"conversationId={setup.ConversationId}&hasLinks=true")).TotalCount);
        Assert.Equal(1, (await P2SearchAsync(setup.FirstClient, $"conversationId={setup.ConversationId}&hasLinks=true")).TotalCount);
        (await setup.FirstClient.PostAsync($"/api/v1/conversations/{setup.ConversationId}/clear-history", null)).EnsureSuccessStatusCode();
        Assert.Equal(0, (await P2SearchAsync(setup.FirstClient, $"conversationId={setup.ConversationId}&hasLinks=true")).TotalCount);
    }
    [Fact]
    public async Task P2SearchTopicFilterCannotEscapeGroupOrRemovedMembership()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var first = await CreateP2TopicAsync(setup.OwnerClient, id, new(Guid.NewGuid(), "اول")); var second = await CreateP2TopicAsync(setup.OwnerClient, id, new(Guid.NewGuid(), "دوم"));
        (await SendP2TopicAsync(setup.OwnerClient, first, Guid.NewGuid(), "اول")).EnsureSuccessStatusCode();
        (await SendP2TopicAsync(setup.OwnerClient, second, Guid.NewGuid(), "دوم")).EnsureSuccessStatusCode();
        Assert.Equal("اول", Assert.Single((await P2SearchAsync(setup.TargetClient, $"topicId={first.TopicId}")).Items).Snippet);
        (await setup.OwnerClient.DeleteAsync($"/api/v1/conversations/{id}/members/{setup.Target.UserId}")).EnsureSuccessStatusCode();
        Assert.Equal(0, (await P2SearchAsync(setup.TargetClient, $"topicId={first.TopicId}")).TotalCount);
    }
    [Fact]
    public async Task P2SearchUnknownSelectorsAndFilterValuesFailSafely()
    {
        using var client = factory.CreateClient(); await CreateMessagingUserAsync(client);
        foreach (var query in new[] { "hasLinks=maybe", $"topicId={Guid.Empty}", "hasAttachment=true&limit=1000", "hasLinks=true&kind=999", "hasLinks=true&cursor=bad" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/messages/search?" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/messages/search")).StatusCode);
    }
    [Fact]
    public async Task P2SearchUsesOneIdentityBatchForMultipleSenders()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        await SendMessageAndReadAsync(setup.OwnerClient, id, Guid.NewGuid(), "p2-query-batch"); await SendMessageAndReadAsync(setup.TargetClient, id, Guid.NewGuid(), "p2-query-batch");
        using var third = factory.CreateClient(); var user = await CreateMessagingUserAsync(third); await factory.AllowPersonalGroupInviteAsync(setup.Owner.UserId, user.UserId);
        (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/members", new AddConversationMemberCommand(user.UserId), JsonOptions)).EnsureSuccessStatusCode();
        await SendMessageAndReadAsync(third, id, Guid.NewGuid(), "p2-query-batch");
        var counter = new P2IdentityQueryCounter();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>(); services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.AddDbContext<MakanDbContext>((_, options) => options.AddInterceptors(counter));
        }));
        using var client = host.CreateClient(); UseBearerToken(client, setup.Owner.AccessToken);
        var result = await P2SearchAsync(client, $"q=p2-query-batch&conversationId={id}");
        Assert.Equal(3, result.Items.Select(i => i.Sender.UserId).Distinct().Count()); Assert.Equal(1, counter.Reads);
    }
    private static async Task<MessageSearchPageResult> P2SearchAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync("/api/v1/messages/search?" + query); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MessageSearchPageResult>(JsonOptions))!;
    }
    private sealed class P2IdentityQueryCounter : DbCommandInterceptor
    {
        private int _reads; public int Reads => Volatile.Read(ref _reads);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            // فقط شمارش یک tag ثابت؛ متن SQL، پارامتر یا دادهٔ کاربر ثبت نمی‌شود.
            if (command.CommandText.Contains("MessagingSafeIdentityBatch", StringComparison.Ordinal)) Interlocked.Increment(ref _reads);
            return ValueTask.FromResult(result);
        }
    }
}
