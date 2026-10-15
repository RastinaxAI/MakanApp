using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task RealInternalProviderPersistsReviewForUnsupportedMediaAndBlocksUnsafeEdits()
    {
        using var senderClient = factory.CreateClient(); using var childClient = factory.CreateClient();
        var sender = await CreateMessagingUserAsync(senderClient); var child = await CreateMessagingUserAsync(childClient);
        await factory.SetCommunicationAgeCategoryAsync(child.UserId, CommunicationAgeCategory.Minor);
        await factory.AllowPersonalGroupInviteAsync(sender.UserId, child.UserId);
        var conversation = await StartDirectAndReadAsync(senderClient, child.UserId, ConversationScope.Personal);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IContentSafetyProvider>(); services.AddSingleton<IContentSafetyProvider, InternalMakanContentSafetyProvider>();
            services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.RemoveAll<IHostedService>();
        }));
        using var realClient = host.CreateClient(); UseBearerToken(realClient, sender.AccessToken);
        var sent = await SendMessageAndReadAsync(realClient, conversation.ConversationId, Guid.NewGuid(), "سلام، تکلیف آماده است");
        using var edit = await realClient.PatchAsJsonAsync($"/api/v1/conversations/{conversation.ConversationId}/messages/{sent.MessageId}",
            new EditMessageCommand("می کشمت", sent.Version), JsonOptions);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, edit.StatusCode);
        Assert.Equal("سلام، تکلیف آماده است", (await factory.GetAdvancedMessageAsync(sent.MessageId)).Text);
        var file = await UploadFileAsync(senderClient, "image/png", "unsupported-policy.png");
        var operation = Guid.NewGuid();
        using var media = await realClient.PostAsJsonAsync($"/api/v1/conversations/{conversation.ConversationId}/messages",
            new SendMessageCommand { ClientMessageId = operation, Kind = MessageKind.Image, AttachmentIds = [file.Id] }, JsonOptions);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, media.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageSafetyReviewRequired, await ReadProblemCodeAsync(media));
        var incident = await factory.GetSafetyIncidentAsync(conversation.ConversationId, sender.UserId, operation);
        Assert.Equal(ContentSafetyDecision.ReviewRequired, incident!.Decision);
        Assert.Contains(InternalMakanContentSafetyProvider.PolicyVersion, (await factory.ReadSafetyPolicyCodesAsync(incident.Id))!);
        Assert.Single(await factory.GetMessagesAsync(conversation.ConversationId));
    }
}
