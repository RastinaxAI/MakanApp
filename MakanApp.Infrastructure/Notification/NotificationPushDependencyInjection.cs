using MakanApp.Application.Notification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.Infrastructure.Notification;

internal static class NotificationPushDependencyInjection
{
    internal static void AddMessengerPushTransports(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new WebPushTransportOptions
        {
            Subject = configuration["Notifications:WebPush:VapidSubject"],
            PublicKey = configuration["Notifications:WebPush:VapidPublicKey"],
            PrivateKey = configuration["Notifications:WebPush:VapidPrivateKey"],
            TimeToLiveSeconds = ReadTimeToLive(configuration["Notifications:WebPush:TimeToLiveSeconds"])
        });
        services.AddSingleton(new FcmTransportOptions
        {
            ProjectId = configuration["Notifications:Fcm:ProjectId"],
            ServiceAccountPath = configuration["Notifications:Fcm:ServiceAccountPath"] ?? configuration["GOOGLE_APPLICATION_CREDENTIALS"],
            TimeToLiveSeconds = ReadTimeToLive(configuration["Notifications:Fcm:TimeToLiveSeconds"])
        });
        services.AddHttpClient("notification-web-push", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(provider => new WebPushResponseSanitizingHandler
            {
                InnerHandler = WebPushNetworkPolicy.CreateHandler(provider.GetRequiredService<PushDeviceOptions>())
            });
        services.AddHttpClient("notification-fcm", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(5),
                PooledConnectionLifetime = TimeSpan.FromMinutes(2), MaxConnectionsPerServer = 20
            });
        services.AddSingleton<IFcmAccessTokenProvider, GoogleFcmAccessTokenProvider>();
        services.AddSingleton(provider => new WebPushNotificationProvider(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("notification-web-push"),
            provider.GetRequiredService<WebPushTransportOptions>(), provider.GetRequiredService<PushDeviceOptions>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton(provider => new FcmPushNotificationProvider(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("notification-fcm"),
            provider.GetRequiredService<IFcmAccessTokenProvider>(), provider.GetRequiredService<FcmTransportOptions>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IPushNotificationProvider, MessengerPushNotificationProvider>();
    }

    private static int ReadTimeToLive(string? value) => value is null ? 3600 : int.TryParse(value, out var seconds) ? seconds : -1;
}
