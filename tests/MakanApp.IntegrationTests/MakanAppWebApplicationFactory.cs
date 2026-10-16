using MakanApp.Application.Identity;
using MakanApp.Application.Ai;
using MakanApp.Application.Commerce;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Application.Platform;
using MakanApp.Application.Storage;
using MakanApp.Api.Realtime;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Reports.Exports;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory :
    WebApplicationFactory<Program>,
    IAsyncLifetime
{
    private const string ConnectionStringEnvironmentVariable =
        "ConnectionStrings__MakanDatabase";

    public const string DatabaseName = "MakanApp_MessagingSafety_IntegrationTests_Step7E";

    private const string TestConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=" + DatabaseName + ";Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    private string _instanceConnectionString = TestConnectionString;
    private string? _recoverySecurityKey;

    private readonly string? _originalConnectionString =
        Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

    public string StorageRootPath { get; } = Path.Combine(
        Path.GetTempPath(),
        "MakanApp-StorageTests",
        Guid.NewGuid().ToString("N"));

    public ControllableTimeProvider Clock { get; } = new();
    public OtpProviderTestControl OtpProvider =>
        Services.GetRequiredService<OtpProviderTestControl>();
    public NotificationDeliveryProviderTestControl NotificationDeliveryProvider =>
        Services.GetRequiredService<NotificationDeliveryProviderTestControl>();
    public PaymentGatewayTestControl PaymentGateway =>
        Services.GetRequiredService<PaymentGatewayTestControl>();
    public SensitiveLogTestControl SensitiveLogs =>
        Services.GetRequiredService<SensitiveLogTestControl>();
    public ContentSafetyProviderTestControl ContentSafetyProvider =>
        Services.GetRequiredService<ContentSafetyProviderTestControl>();
    public PlatformStepUpTestControl PlatformStepUp =>
        Services.GetRequiredService<PlatformStepUpTestControl>();
    public AiServiceTestControl AiService =>
        Services.GetRequiredService<AiServiceTestControl>();

    public MakanAppWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(
            ConnectionStringEnvironmentVariable,
            TestConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MakanDatabase"] = _instanceConnectionString,
                ["Sms:Provider"] = "Development",
                ["Identity:Otp:MaxRequestsPerWindow"] = "5",
                ["Identity:Session:AccessLifetimeSeconds"] = "86400",
                ["Identity:Session:RefreshLifetimeSeconds"] = "2592000",
                ["Storage:RootPath"] = StorageRootPath,
                ["Storage:MaxFileSizeBytes"] = "1024",
                ["Storage:UnattachedLifetimeHours"] = "1",
                ["Storage:AllowedContentTypes:0"] = "text/plain",
                ["Storage:AllowedContentTypes:1"] = "application/pdf",
                ["Storage:AllowedContentTypes:2"] = "image/png",
                ["Storage:AllowedContentTypes:3"] = "video/mp4",
                ["Storage:AllowedContentTypes:4"] = "audio/mpeg"
            });
            if (_recoverySecurityKey is not null)
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Identity:SecurityKey"] = _recoverySecurityKey
                });
            }
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<MessagingEphemeralTestControl>();
            services.RemoveAll<IMessagingEphemeralNotifier>();
            services.AddScoped<IMessagingEphemeralNotifier>(provider => new RecordingMessagingEphemeralNotifier(
                provider.GetRequiredService<SignalRMessagingEphemeralNotifier>(), provider.GetRequiredService<MessagingEphemeralTestControl>()));
            var ephemeralDispatcher = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(MessagingEphemeralDispatcher));
            if (ephemeralDispatcher is not null) services.Remove(ephemeralDispatcher);
            var scheduledDispatcher = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(ScheduledMessageDispatcher));
            if (scheduledDispatcher is not null) services.Remove(scheduledDispatcher);
            var reportDispatcher = services.FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(ReportExportDispatcher));
            if (reportDispatcher is not null)
            {
                services.Remove(reportDispatcher);
            }

            var outboxDispatcher = services.FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(OutboxDispatcher));
            if (outboxDispatcher is not null)
            {
                services.Remove(outboxDispatcher);
            }

            var notificationDeliveryDispatcher = services.FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(NotificationDeliveryDispatcher));
            if (notificationDeliveryDispatcher is not null)
            {
                services.Remove(notificationDeliveryDispatcher);
            }

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<SessionOptions>();
            services.AddSingleton(new SessionOptions
            {
                AccessLifetime = TimeSpan.FromDays(1),
                RefreshLifetime = TimeSpan.FromDays(30),
                MaximumDeviceNameLength = 100
            });
            services.RemoveAll<StorageOptions>();
            services.AddSingleton(new StorageOptions
            {
                RootPath = StorageRootPath,
                MaxFileSizeBytes = 1024,
                UnattachedLifetime = TimeSpan.FromHours(1),
                AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "text/plain",
                    "application/pdf",
                    "image/png",
                    "video/mp4",
                    "audio/mpeg"
                }
            });
            services.AddSingleton<StorageFailureSwitch>();
            services.AddSingleton<RealtimeFailureSwitch>();
            services.AddSingleton<OtpProviderTestControl>();
            services.AddSingleton<NotificationDeliveryProviderTestControl>();
            services.AddSingleton<PaymentGatewayTestControl>();
            services.AddSingleton<SensitiveLogTestControl>();
            services.AddSingleton<ContentSafetyProviderTestControl>();
            services.AddSingleton<PlatformStepUpTestControl>();
            services.AddSingleton<AiServiceTestControl>();
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider, RecordingTestLoggerProvider>();
            services.RemoveAll<DevelopmentOtpStore>();
            services.RemoveAll<DevelopmentOtpProvider>();
            services.AddSingleton<DevelopmentOtpStore>();
            services.AddSingleton<DevelopmentOtpProvider>();
            services.RemoveAll<IOtpDeliveryProvider>();
            services.AddSingleton<IOtpDeliveryProvider, FaultInjectingOtpDeliveryProvider>();
            services.RemoveAll<IPushNotificationProvider>();
            services.AddSingleton<IPushNotificationProvider, RecordingPushNotificationProvider>();
            services.RemoveAll<INotificationSmsProvider>();
            services.AddSingleton<INotificationSmsProvider, RecordingNotificationSmsProvider>();
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway, RecordingPaymentGateway>();
            services.RemoveAll<IContentSafetyProvider>();
            services.AddSingleton<IContentSafetyProvider, RecordingContentSafetyProvider>();
            services.RemoveAll<IPlatformStepUpAuthorization>();
            services.AddSingleton<IPlatformStepUpAuthorization, TestPlatformStepUpAuthorization>();
            services.RemoveAll<IPlatformMfaAssurance>();
            services.AddSingleton<IPlatformMfaAssurance, TestPlatformStepUpAuthorization>();
            services.RemoveAll<IAiServiceClient>();
            services.AddSingleton<IAiServiceClient, RecordingAiServiceClient>();
            services.RemoveAll<NotificationDeliveryOptions>();
            services.RemoveAll<PushDeviceOptions>();
            services.AddSingleton(new PushDeviceOptions { AllowedWebPushHosts = ["push.example.test"] });
            services.AddSingleton(new NotificationDeliveryOptions
            {
                BatchSize = 50,
                ClaimLease = TimeSpan.FromSeconds(1),
                IdlePollingInterval = TimeSpan.FromMilliseconds(10),
                InitialRetryDelay = TimeSpan.FromSeconds(1),
                MaximumRetryDelay = TimeSpan.FromSeconds(5),
                ProviderTimeout = TimeSpan.FromMilliseconds(100),
                MaximumAttempts = 3
            });
            services.RemoveAll<IMessagingRealtimeNotifier>();
            services.AddScoped<IMessagingRealtimeNotifier>(serviceProvider =>
                new FaultInjectingMessagingRealtimeNotifier(
                    serviceProvider.GetRequiredService<SignalRMessagingRealtimeNotifier>(),
                    serviceProvider.GetRequiredService<RealtimeFailureSwitch>()));
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(serviceProvider =>
                new FaultInjectingFileStorage(
                    serviceProvider.GetRequiredService<LocalFileStorage>(),
                    serviceProvider.GetRequiredService<StorageFailureSwitch>()));
            if (_recoverySecurityKey is not null)
            {
                services.RemoveAll<IIdentitySecurity>();
                services.AddSingleton<IIdentitySecurity>(new IdentityCryptography(
                    System.Text.Encoding.UTF8.GetBytes(_recoverySecurityKey)));
                services.RemoveAll<IHostedService>();
            }
        });
    }

    public sealed class ControllableTimeProvider : TimeProvider
    {
        private readonly object _sync = new();
        private DateTimeOffset? _utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            lock (_sync)
            {
                return _utcNow ?? DateTimeOffset.UtcNow;
            }
        }

        public void SetUtcNow(DateTime utcNow)
        {
            if (utcNow.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("زمان تست باید UTC باشد.", nameof(utcNow));
            }

            lock (_sync)
            {
                _utcNow = new DateTimeOffset(utcNow);
            }
        }

        public void UseSystemTime()
        {
            lock (_sync)
            {
                _utcNow = null;
            }
        }
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
            Dispose();
        }
        finally
        {
            try
            {
                DeleteStorageRoot();
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    ConnectionStringEnvironmentVariable,
                    _originalConnectionString);
            }
        }
    }

    private void DeleteStorageRoot()
    {
        var expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MakanApp-StorageTests"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolvedRoot = Path.GetFullPath(StorageRootPath);
        if (resolvedRoot.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(resolvedRoot))
        {
            Directory.Delete(resolvedRoot, true);
        }
    }

    public string GetLatestOtpCode(string phoneNumber)
    {
        if (!PhoneNumberNormalizer.TryNormalize(phoneNumber, out var normalizedPhoneNumber))
        {
            throw new ArgumentException("شماره تست OTP معتبر نیست.", nameof(phoneNumber));
        }

        return OtpProvider.GetLatestCode(normalizedPhoneNumber);
    }

    public async Task ExpireChallengeAsync(Guid challengeId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var expiredAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [identity].[OtpChallenges] SET [ExpiresAtUtc] = {expiredAtUtc} WHERE [Id] = {challengeId}");
    }

    public async Task<int> CountOtpChallengesAsync(string normalizedPhoneNumber)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.OtpChallenges.CountAsync(
            challenge => challenge.NormalizedPhoneNumber == normalizedPhoneNumber);
    }

    public async Task<bool> IsChallengeProtectedWithCodeAsync(
        Guid challengeId,
        string code)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var security = scope.ServiceProvider.GetRequiredService<IIdentitySecurity>();
        var challenge = await dbContext.OtpChallenges
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == challengeId);
        return challenge.CodeHash.SequenceEqual(security.HashOtp(code, challenge.Salt));
    }

    public async Task<int> CountUsersForPhoneAsync(string normalizedPhoneNumber)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.UserCredentials.CountAsync(
            credential => credential.Kind == CredentialKind.MobilePhone &&
                          credential.NormalizedIdentifier == normalizedPhoneNumber);
    }

    public async Task<bool> IsSessionRevokedAsync(string rawToken)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var security = scope.ServiceProvider.GetRequiredService<IIdentitySecurity>();
        var tokenHash = security.HashSessionToken(rawToken);
        var session = await dbContext.UserSessions.SingleAsync(
            candidate => candidate.TokenHash.SequenceEqual(tokenHash));
        return session.RevokedAtUtc.HasValue;
    }
}
