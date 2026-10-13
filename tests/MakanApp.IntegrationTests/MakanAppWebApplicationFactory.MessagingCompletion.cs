using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<int> CountSavedConversationsAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Conversations.CountAsync(conversation =>
            conversation.Type == ConversationType.Saved &&
            conversation.SavedOwnerUserId == userId);
    }

    public async Task<SavedConversationDatabaseRecord> GetSavedConversationAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.Type == ConversationType.Saved &&
                                   conversation.SavedOwnerUserId == userId)
            .Select(conversation => new SavedConversationDatabaseRecord(
                conversation.Id,
                conversation.Scope,
                conversation.OrganizationId,
                conversation.SavedOwnerUserId))
            .SingleAsync();
    }

    public async Task<MessageNotificationDatabaseRecord?> GetMessageNotificationAsync(
        Guid messageId,
        Guid recipientUserId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.SourceEventId == messageId &&
                                   notification.RecipientUserId == recipientUserId)
            .Select(notification => new MessageNotificationDatabaseRecord(
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Body,
                notification.ResourceType,
                notification.ResourceId))
            .SingleOrDefaultAsync();
    }
}

public sealed record SavedConversationDatabaseRecord(
    Guid ConversationId,
    ConversationScope Scope,
    Guid? OrganizationId,
    Guid? SavedOwnerUserId);

public sealed record MessageNotificationDatabaseRecord(
    Guid NotificationId,
    NotificationType Type,
    string Title,
    string Body,
    NotificationResourceType ResourceType,
    Guid ResourceId);
