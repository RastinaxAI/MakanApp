using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    internal async Task<NotificationIntent> CreateDeliveryAcademicIntentAsync(Guid user, Guid? organization, NotificationType type, DateTime now)
    {
        await using var scope = Services.CreateAsyncScope();
        return await NotificationAcademicFixture.CreateAsync(scope.ServiceProvider.GetRequiredService<MakanDbContext>(), user, organization, type, now);
    }
}
