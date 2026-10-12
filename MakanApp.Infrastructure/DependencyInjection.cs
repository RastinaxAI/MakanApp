using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Application.Outbox;
using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Dashboards;
using MakanApp.Application.Reports.Detailed;
using MakanApp.Application.Reports.Exports;
using MakanApp.Application.Storage;
using MakanApp.Infrastructure.Academic;
using MakanApp.Infrastructure.Assessment;
using MakanApp.Infrastructure.Guardian;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Identity.Otp.MeliPayamak;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Organization;
using MakanApp.Infrastructure.Reports.Dashboards;
using MakanApp.Infrastructure.Reports.Detailed;
using MakanApp.Infrastructure.Reports.Exports;
using MakanApp.Infrastructure.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        var connectionString = configuration.GetConnectionString("MakanDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:MakanDatabase must be configured before the application starts.");
        }

        services.AddDbContext<MakanDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IIdentityStore, EfIdentityStore>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IOrganizationStore, EfOrganizationStore>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<IOrganizationService>(serviceProvider =>
            serviceProvider.GetRequiredService<OrganizationService>());
        services.AddScoped<IGuardianStore, EfGuardianStore>();
        services.AddScoped<IGuardianService, GuardianService>();
        services.AddScoped<IAcademicStore, EfAcademicStore>();
        services.AddScoped<IAcademicService, AcademicService>();
        services.AddScoped<IAcademicSessionStore, EfAcademicSessionStore>();
        services.AddScoped<IAcademicSessionService, AcademicSessionService>();
        services.AddScoped<IAssignmentStore, EfAssignmentStore>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<ISubmissionStore, EfSubmissionStore>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IEvaluationStore, EfEvaluationStore>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddScoped<IExamStore, EfExamStore>();
        services.AddScoped<IExamService, ExamService>();
        services.AddScoped<IExamAttemptStore, EfExamAttemptStore>();
        services.AddScoped<IExamAttemptService, ExamAttemptService>();
        services.AddSingleton<IExamQuestionOrderRandomizer, SecureExamQuestionOrderRandomizer>();
        services.AddScoped<IExamAnswerStore, EfExamAnswerStore>();
        services.AddScoped<IExamAnswerService, ExamAnswerService>();
        services.AddScoped<IExamFinalizationStore, EfExamFinalizationStore>();
        services.AddScoped<IExamFinalizationService, ExamFinalizationService>();
        services.AddScoped<IExamGradingStore, EfExamGradingStore>();
        services.AddScoped<IExamGradingService, ExamGradingService>();
        services.AddScoped<IDashboardReadService, EfDashboardReadService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IDetailedReportReadService, EfDetailedReportReadService>();
        services.AddScoped<DetailedReportService>();
        services.AddScoped<IDetailedReportService>(serviceProvider =>
            serviceProvider.GetRequiredService<DetailedReportService>());
        services.AddScoped<IReportExportDataSource>(serviceProvider =>
            serviceProvider.GetRequiredService<DetailedReportService>());
        services.AddScoped<IReportExportJobStore, EfReportExportJobStore>();
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<IReportExporter, CsvReportExporter>();
        services.AddScoped<IReportExportOutputStore, ReportExportOutputStore>();
        services.AddScoped<ReportExportProcessor>();
        services.AddSingleton<IReportExportQueueSignal, ReportExportQueueSignal>();
        services.AddHostedService<ReportExportDispatcher>();
        services.AddScoped<EfMessagingStore>();
        services.AddScoped<IMessagingStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfMessagingStore>());
        services.AddScoped<IConversationManagementStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfMessagingStore>());
        services.AddScoped<IMessagingSafetyStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfMessagingStore>());
        services.AddScoped<IMessagingService, MessagingService>();
        services.AddScoped<IConversationManagementService, ConversationManagementService>();
        services.AddScoped<IMessagingSafetyService, MessagingSafetyService>();
        services.AddScoped<IMessagingRealtimeAudienceResolver, MessagingRealtimeAudienceResolver>();
        services.AddSingleton<MessagingOutboxWakeSignal>();
        services.AddScoped<MessagingRealtimeOutboxProcessor>();
        services.AddHostedService<MessagingRealtimeOutboxDispatcher>();
        services.AddScoped<INotificationStore, EfNotificationStore>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IOutbox, EfOutbox>();
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddScoped<IFileAssetStore, EfFileAssetStore>();
        services.AddScoped<IFileAssetBoundAccessResolver, EfFileAssetBoundAccessResolver>();
        services.AddScoped<IStorageService, StorageService>();
        services.AddScoped<IAccessContextResolver, GuardianAccessContextResolver>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(CreateOtpOptions(configuration, environmentName));
        services.AddSingleton(CreateSessionOptions(configuration));
        services.AddSingleton(CreateMessagingOptions(configuration));
        services.AddSingleton<ICommunicationEligibilityPolicy, CommunicationEligibilityPolicy>();
        services.AddSingleton<IConversationManagementPolicy, ConversationManagementAuthorizationPolicy>();
        var storageOptions = CreateStorageOptions(configuration);
        services.AddSingleton(storageOptions);
        services.AddSingleton(CreateReportExportOptions(configuration));
        services.AddSingleton<FileUploadPolicy>();
        services.AddSingleton<IIdentitySecurity>(
            new IdentityCryptography(CreateSecurityKey(
                configuration,
                environmentName,
                UsesManagedOtpProvider(configuration))));
        AddOtpDeliveryProvider(services, configuration, environmentName);

        if (IsDevelopmentLike(environmentName) || !string.IsNullOrWhiteSpace(storageOptions.RootPath))
        {
            services.AddSingleton<LocalFileStorage>();
            services.AddSingleton<IFileStorage>(serviceProvider =>
                serviceProvider.GetRequiredService<LocalFileStorage>());
        }
        else
        {
            services.AddSingleton<IFileStorage, UnavailableFileStorage>();
        }

        return services;
    }

    private static void AddOtpDeliveryProvider(
        IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        const string developmentProvider = "Development";
        const string unavailableProvider = "Unavailable";
        const string meliPayamakOtpProvider = "MeliPayamakOtp";
        var configuredProvider = configuration["Sms:Provider"]?.Trim();
        var provider = string.IsNullOrWhiteSpace(configuredProvider)
            ? IsDevelopmentLike(environmentName) ? developmentProvider : unavailableProvider
            : configuredProvider;

        if (provider.Equals(developmentProvider, StringComparison.OrdinalIgnoreCase))
        {
            if (!IsDevelopmentLike(environmentName))
            {
                throw new InvalidOperationException(
                    "Sms:Provider=Development is allowed only in Development and Testing environments.");
            }

            services.AddSingleton<DevelopmentOtpStore>();
            services.AddSingleton<DevelopmentOtpProvider>();
            services.AddSingleton<IOtpDeliveryProvider>(serviceProvider =>
                serviceProvider.GetRequiredService<DevelopmentOtpProvider>());
            return;
        }

        if (provider.Equals(meliPayamakOtpProvider, StringComparison.OrdinalIgnoreCase))
        {
            var options = CreateMeliPayamakOtpOptions(configuration);
            var baseUri = options.ValidateAndGetBaseUri();
            services.AddSingleton(options);
            services
                .AddHttpClient<MeliPayamakOtpProvider>(client =>
                {
                    client.BaseAddress = baseUri;
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                })
                .RemoveAllLoggers();
            services.AddTransient<IOtpDeliveryProvider>(serviceProvider =>
                serviceProvider.GetRequiredService<MeliPayamakOtpProvider>());
            return;
        }

        if (provider.Equals(unavailableProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IOtpDeliveryProvider, UnavailableOtpDeliveryProvider>();
            return;
        }

        throw new InvalidOperationException(
            "Sms:Provider must be Development, MeliPayamakOtp or Unavailable.");
    }

    private static MeliPayamakOtpOptions CreateMeliPayamakOtpOptions(
        IConfiguration configuration)
    {
        var timeoutValue = configuration[$"{MeliPayamakOtpOptions.SectionName}:TimeoutSeconds"];
        var timeoutSeconds = string.IsNullOrWhiteSpace(timeoutValue)
            ? MeliPayamakOtpOptions.DefaultTimeoutSeconds
            : int.TryParse(timeoutValue, out var parsedTimeout) ? parsedTimeout : -1;
        return new MeliPayamakOtpOptions
        {
            BaseUrl = configuration[$"{MeliPayamakOtpOptions.SectionName}:BaseUrl"]
                ?? MeliPayamakOtpOptions.DefaultBaseUrl,
            ServiceToken = configuration[$"{MeliPayamakOtpOptions.SectionName}:ServiceToken"]
                ?? string.Empty,
            TimeoutSeconds = timeoutSeconds
        };
    }

    private static OtpOptions CreateOtpOptions(
        IConfiguration configuration,
        string environmentName)
    {
        var minimumCodeLength = ReadPositiveInt(
            configuration,
            "Identity:Otp:MinimumCodeLength",
            4);
        var maximumCodeLength = ReadPositiveInt(
            configuration,
            "Identity:Otp:MaximumCodeLength",
            64);
        if (minimumCodeLength > maximumCodeLength || maximumCodeLength > 256)
        {
            throw new InvalidOperationException(
                "Identity OTP code length must define a valid range up to 256 characters.");
        }

        return new OtpOptions
        {
            Lifetime = TimeSpan.FromSeconds(ReadPositiveInt(
                configuration,
                "Identity:Otp:LifetimeSeconds",
                300)),
            ResendDelay = TimeSpan.FromSeconds(
                environmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : ReadPositiveInt(configuration, "Identity:Otp:ResendDelaySeconds", 30)),
            RateLimitWindow = TimeSpan.FromSeconds(ReadPositiveInt(
                configuration,
                "Identity:Otp:RateLimitWindowSeconds",
                3600)),
            MaxRequestsPerWindow = ReadPositiveInt(
                configuration,
                "Identity:Otp:MaxRequestsPerWindow",
                environmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase) ? 1000 : 5),
            MaxFailedAttempts = ReadPositiveInt(
                configuration,
                "Identity:Otp:MaxFailedAttempts",
                5),
            MinimumCodeLength = minimumCodeLength,
            MaximumCodeLength = maximumCodeLength
        };
    }

    private static SessionOptions CreateSessionOptions(IConfiguration configuration) => new()
    {
        Lifetime = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            "Identity:Session:LifetimeSeconds",
            2_592_000))
    };

    private static StorageOptions CreateStorageOptions(IConfiguration configuration)
    {
        var configuredTypes = configuration
            .GetSection($"{StorageOptions.SectionName}:AllowedContentTypes")
            .GetChildren()
            .Select(item => item.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new StorageOptions
        {
            RootPath = configuration[$"{StorageOptions.SectionName}:RootPath"] ?? string.Empty,
            MaxFileSizeBytes = ReadPositiveLong(
                configuration,
                $"{StorageOptions.SectionName}:MaxFileSizeBytes",
                10 * 1024 * 1024),
            AllowedContentTypes = configuredTypes.Count > 0
                ? configuredTypes
                : new StorageOptions().AllowedContentTypes,
            UnattachedLifetime = TimeSpan.FromHours(ReadPositiveInt(
                configuration,
                $"{StorageOptions.SectionName}:UnattachedLifetimeHours",
                24))
        };
    }

    private static MessagingOptions CreateMessagingOptions(IConfiguration configuration)
    {
        var maximumTextLength = ReadPositiveInt(
            configuration,
            $"{MessagingOptions.SectionName}:MaximumTextLength",
            MakanApp.Domain.Messaging.Message.StorageMaximumTextLength);
        if (maximumTextLength > MakanApp.Domain.Messaging.Message.StorageMaximumTextLength)
        {
            throw new InvalidOperationException(
                $"Messaging:MaximumTextLength cannot exceed {MakanApp.Domain.Messaging.Message.StorageMaximumTextLength}.");
        }

        return new MessagingOptions
        {
            MaximumTextLength = maximumTextLength,
            DefaultHistoryLimit = ReadPositiveInt(
                configuration,
                $"{MessagingOptions.SectionName}:DefaultHistoryLimit",
                50),
            MaximumHistoryLimit = ReadPositiveInt(
                configuration,
                $"{MessagingOptions.SectionName}:MaximumHistoryLimit",
                100),
            MaximumAttachmentsPerMessage = ReadPositiveInt(
                configuration,
                $"{MessagingOptions.SectionName}:MaximumAttachmentsPerMessage",
                10),
            MaximumMentionsPerMessage = ReadPositiveInt(
                configuration,
                $"{MessagingOptions.SectionName}:MaximumMentionsPerMessage",
                50),
            DefaultChangeLimit = ReadPositiveInt(
                configuration,
                $"{MessagingOptions.SectionName}:DefaultChangeLimit",
                100),
            MaximumChangeLimit = ReadPositiveInt(
                configuration,
                $"{MessagingOptions.SectionName}:MaximumChangeLimit",
                200),
            MaximumSearchQueryLength = ReadPositiveInt(
                configuration,
                $"{MessagingOptions.SectionName}:MaximumSearchQueryLength",
                200),
            DefaultSearchLimit = ReadPositiveInt(
                configuration,
                $"{MessagingOptions.SectionName}:DefaultSearchLimit",
                20),
            MaximumSearchLimit = ReadPositiveInt(
                configuration,
                $"{MessagingOptions.SectionName}:MaximumSearchLimit",
                50)
        };
    }

    private static ReportExportOptions CreateReportExportOptions(IConfiguration configuration) => new()
    {
        MaximumRows = ReadPositiveInt(
            configuration,
            $"{ReportExportOptions.SectionName}:MaximumRows",
            10_000),
        PageSize = Math.Min(100, ReadPositiveInt(
            configuration,
            $"{ReportExportOptions.SectionName}:PageSize",
            100)),
        MaximumFileSizeBytes = ReadPositiveLong(
            configuration,
            $"{ReportExportOptions.SectionName}:MaximumFileSizeBytes",
            20 * 1024 * 1024),
        ClaimLease = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            $"{ReportExportOptions.SectionName}:ClaimLeaseSeconds",
            120)),
        IdlePollingInterval = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            $"{ReportExportOptions.SectionName}:IdlePollingSeconds",
            2)),
        RetryDelay = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            $"{ReportExportOptions.SectionName}:RetryDelaySeconds",
            10)),
        MaximumAttempts = ReadPositiveInt(
            configuration,
            $"{ReportExportOptions.SectionName}:MaximumAttempts",
            3)
    };

    private static byte[] CreateSecurityKey(
        IConfiguration configuration,
        string environmentName,
        bool requiresPersistentKey)
    {
        var configuredKey = configuration["Identity:SecurityKey"];
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            var keyBytes = Encoding.UTF8.GetBytes(configuredKey);
            if (keyBytes.Length < 32)
            {
                throw new InvalidOperationException(
                    "Identity:SecurityKey must contain at least 32 UTF-8 bytes.");
            }

            return keyBytes;
        }

        if (requiresPersistentKey)
        {
            throw new InvalidOperationException(
                "Identity:SecurityKey must be supplied by a secret store when MeliPayamakOtp is selected so persisted OTP challenges remain verifiable across process restarts.");
        }

        if (!IsDevelopmentLike(environmentName))
        {
            throw new InvalidOperationException(
                "Identity:SecurityKey must be supplied by a secret store outside Development and Testing.");
        }

        return RandomNumberGenerator.GetBytes(32);
    }

    private static bool UsesManagedOtpProvider(IConfiguration configuration) =>
        string.Equals(
            configuration["Sms:Provider"]?.Trim(),
            "MeliPayamakOtp",
            StringComparison.OrdinalIgnoreCase);

    private static int ReadPositiveInt(
        IConfiguration configuration,
        string key,
        int fallback)
    {
        var value = configuration[key];
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static long ReadPositiveLong(
        IConfiguration configuration,
        string key,
        long fallback)
    {
        var value = configuration[key];
        return long.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static bool IsDevelopmentLike(string environmentName) =>
        environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase) ||
        environmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase);
}
