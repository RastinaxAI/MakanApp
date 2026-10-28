using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Academic;
using MakanApp.Application.Ai;
using MakanApp.Application.Assessment;
using MakanApp.Application.Commerce;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Application.Outbox;
using MakanApp.Application.Organization;
using MakanApp.Application.Platform;
using MakanApp.Application.Reports.Dashboards;
using MakanApp.Application.Reports.Detailed;
using MakanApp.Application.Reports.Exports;
using MakanApp.Application.Storage;
using MakanApp.Infrastructure.Academic;
using MakanApp.Infrastructure.Ai;
using MakanApp.Infrastructure.Assessment;
using MakanApp.Infrastructure.Commerce;
using MakanApp.Infrastructure.Guardian;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Identity.Otp.MeliPayamak;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Organization;
using MakanApp.Infrastructure.Platform;
using MakanApp.Infrastructure.Reports.Dashboards;
using MakanApp.Infrastructure.Reports.Detailed;
using MakanApp.Infrastructure.Reports.Exports;
using MakanApp.Infrastructure.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        services.AddSingleton<EfCoreLoggingSafety>();
        services.AddDbContext<MakanDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString);
            provider.GetRequiredService<EfCoreLoggingSafety>().Configure(options, environmentName);
        });

        services.AddMetrics();
        services.AddSingleton<WorkerTelemetry>();
        services.AddSingleton(OperationalMetricsOptions.FromConfiguration(configuration));
        services.AddHostedService<OperationalMetricsPublisher>();

        services.AddScoped<IIdentityStore, EfIdentityStore>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ProfilePhotoService>();
        services.AddSingleton<IIdentitySecurityEventRecorder, LoggingIdentitySecurityEventRecorder>();
        services.AddScoped<IOrganizationStore, EfOrganizationStore>();
        services.AddScoped<SchoolManagementAccessResolver>();
        services.AddScoped<ICommunicationAgeStore, EfCommunicationAgeStore>();
        services.AddScoped<CommunicationAgeService>();
        services.AddScoped<ConversationInvitationService>();
        services.AddScoped<OfficialClassConversationService>();
        services.AddScoped<IOfficialClassConversationStore>(sp => sp.GetRequiredService<EfMessagingStore>());
        services.AddScoped<IConversationInvitationStore>(sp => sp.GetRequiredService<EfMessagingStore>());
        services.AddScoped<IOrganizationPersonStore, EfOrganizationPersonStore>();
        services.AddScoped<IOrganizationPersonService, OrganizationPersonService>();
        services.AddScoped<IGuardianManagementStore, EfGuardianManagementStore>();
        services.AddScoped<IGuardianManagementService, GuardianManagementService>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<IOrganizationService>(serviceProvider =>
            serviceProvider.GetRequiredService<OrganizationService>());
        services.AddScoped<IPlatformIamStore, EfPlatformIamStore>();
        services.AddScoped<PlatformIamService>();
        services.AddScoped<IPlatformIamService>(serviceProvider =>
            serviceProvider.GetRequiredService<PlatformIamService>());
        services.AddScoped<IPlatformPermissionResolver>(serviceProvider =>
            serviceProvider.GetRequiredService<PlatformIamService>());
        services.AddScoped<IPlatformBootstrapService, PlatformBootstrapService>();
        services.AddScoped<IPlatformOperationsStore, EfPlatformOperationsStore>();
        services.AddScoped<IPlatformOperationsService, PlatformOperationsService>();
        services.AddScoped<IPlatformEducationSafetyStore, EfPlatformEducationSafetyStore>();
        services.AddScoped<IPlatformEducationSafetyService, PlatformEducationSafetyService>();
        services.AddScoped<IPlatformCommerceOperationsStore, EfPlatformCommerceOperationsStore>();
        services.AddScoped<IPlatformCommerceOperationsService, PlatformCommerceOperationsService>();
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
        services.AddScoped<ICommerceStore, EfCommerceStore>();
        services.AddScoped<CommerceService>();
        services.AddScoped<ICommerceService>(serviceProvider =>
            serviceProvider.GetRequiredService<CommerceService>());
        services.AddScoped<IEntitlementService>(serviceProvider =>
            serviceProvider.GetRequiredService<CommerceService>());
        services.AddScoped<IOrderPaymentStore, EfOrderPaymentStore>();
        services.AddScoped<OrderPaymentService>();
        services.AddScoped<IOrderPaymentService>(serviceProvider =>
            serviceProvider.GetRequiredService<OrderPaymentService>());
        services.AddScoped<IPaymentVerificationService>(serviceProvider =>
            serviceProvider.GetRequiredService<OrderPaymentService>());
        services.AddScoped<ISubscriptionActivationHandler, EfSubscriptionActivationHandler>();
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
        services.AddScoped<IModerationStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfMessagingStore>());
        services.AddScoped<IMessagingPrivacyResolver, EfMessagingPrivacyResolver>();
        services.AddScoped<IMessagingDirectoryStore, EfMessagingDirectoryStore>();
        services.AddScoped<IMessagingService, MessagingService>();
        var ephemeralOptions = new MessagingEphemeralOptions
        {
            TypingTtl = TimeSpan.FromSeconds(ReadPositiveInt(configuration, "Messaging:Ephemeral:TypingTtlSeconds", 6)),
            TypingRefreshInterval = TimeSpan.FromSeconds(ReadPositiveInt(configuration, "Messaging:Ephemeral:TypingRefreshSeconds", 3)),
            ConnectionLease = TimeSpan.FromSeconds(ReadPositiveInt(configuration, "Messaging:Ephemeral:ConnectionLeaseSeconds", 45)),
            HeartbeatInterval = TimeSpan.FromSeconds(ReadPositiveInt(configuration, "Messaging:Ephemeral:HeartbeatSeconds", 10)),
            RecentlyWindow = TimeSpan.FromSeconds(ReadPositiveInt(configuration, "Messaging:Ephemeral:RecentlyWindowSeconds", 300)),
            LastSeenRetention = TimeSpan.FromSeconds(ReadPositiveInt(configuration, "Messaging:Ephemeral:LastSeenRetentionSeconds", 86400)),
            MaximumConnections = ReadPositiveInt(configuration, "Messaging:Ephemeral:MaximumConnections", 10000),
            MaximumConnectionsPerUser = ReadPositiveInt(configuration, "Messaging:Ephemeral:MaximumConnectionsPerUser", 10),
            MaximumSubscriptionsPerConnection = ReadPositiveInt(configuration, "Messaging:Ephemeral:MaximumSubscriptionsPerConnection", 50),
            CommandsPerMinute = ReadPositiveInt(configuration, "Messaging:Ephemeral:CommandsPerMinute", 120)
        };
        ephemeralOptions.Validate();
        services.AddSingleton(ephemeralOptions);
        services.AddSingleton<IMessagingEphemeralState, InMemoryMessagingEphemeralState>();
        services.AddScoped<IMessagingEphemeralAccessStore>(provider => provider.GetRequiredService<EfMessagingStore>());
        services.AddScoped<MessagingEphemeralService>();
        services.AddScoped<IMessagingPresentationStore>(provider => provider.GetRequiredService<EfMessagingStore>());
        services.AddScoped<MessagingPresentationService>();
        services.AddScoped<IConversationAdvancedManagementStore>(provider => provider.GetRequiredService<EfMessagingStore>());
        services.AddScoped<ConversationAdvancedManagementService>();
        services.AddScoped<IConversationTopicStore>(provider => provider.GetRequiredService<EfMessagingStore>());
        services.AddScoped<ConversationTopicService>();
        services.AddScoped<ConversationPollService>();
        services.AddScoped<ConversationChecklistService>();
        services.AddScoped<IConversationChecklistStore>(sp => sp.GetRequiredService<EfMessagingStore>());
        services.AddScoped<IConversationPollStore>(sp => sp.GetRequiredService<EfMessagingStore>());
        services.AddScoped<IScheduledMessageStore>(provider => provider.GetRequiredService<EfMessagingStore>());
        services.AddScoped<ScheduledMessageService>();
        services.AddScoped<ScheduledMessageProcessor>();
        services.AddHostedService<ScheduledMessageDispatcher>();
        services.AddHostedService<MessagingEphemeralDispatcher>();
        services.AddScoped<IConversationManagementService, ConversationManagementService>();
        services.AddScoped<IMessagingSafetyService, MessagingSafetyService>();
        services.AddScoped<IModerationService, ModerationService>();
        services.AddScoped<IMessagingDirectoryService, MessagingDirectoryService>();
        services.AddScoped<IMessagingRealtimeAudienceResolver, MessagingRealtimeAudienceResolver>();
        services.AddSingleton<MessagingOutboxWakeSignal>();
        services.AddScoped<MessagingRealtimeOutboxProcessor>();
        services.AddHostedService<MessagingRealtimeOutboxDispatcher>();
        services.AddScoped<INotificationStore, EfNotificationStore>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<EfNotificationPreferenceStore>();
        services.AddScoped<INotificationPreferenceStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfNotificationPreferenceStore>());
        services.AddScoped<IPushDeviceStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfNotificationPreferenceStore>());
        services.AddScoped<INotificationDeliveryStore>(serviceProvider =>
            serviceProvider.GetRequiredService<EfNotificationPreferenceStore>());
        services.AddScoped<INotificationPreferenceResolver, NotificationPreferenceResolver>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<IPushDeviceService, PushDeviceService>();
        services.AddSingleton(new PushDeviceOptions
        {
            AllowedWebPushHosts = configuration.GetSection("Notifications:WebPush:AllowedEndpointHosts")
                .GetChildren().Select(item => item.Value?.Trim()).Where(value => !string.IsNullOrEmpty(value))
                .Select(value => value!).ToArray()
        });
        services.AddScoped<INotificationDeliveryCoordinator, NotificationDeliveryCoordinator>();
        services.AddScoped<IOutbox, EfOutbox>();
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddScoped<NotificationDeliveryProcessor>();
        services.AddHostedService<NotificationDeliveryDispatcher>();
        services.AddScoped<IFileAssetStore, EfFileAssetStore>();
        services.AddScoped<IFileAssetBoundAccessResolver, EfFileAssetBoundAccessResolver>();
        services.AddScoped<IStorageService, StorageService>();
        services.AddSingleton<IFileContentValidator, FileSignatureValidator>();
        services.AddScoped<IAccessContextResolver, GuardianAccessContextResolver>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(CreateAiServiceAuthenticationOptions(configuration));
        services.AddSingleton(CreateAiServiceOptions(configuration));
        services.AddSingleton<AiServiceCircuitBreaker>();
        services.AddSingleton<IAiServiceCredentialProvider, HmacAiServiceCredentialProvider>();
        services.AddHttpClient<IAiServiceClient, AiServiceClient>((provider, client) =>
            client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<AiServiceOptions>().TimeoutSeconds));
        services.AddScoped<IAiScopeStore, EfAiScopeStore>();
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<IStudentContextBuilder, StudentContextBuilder>();
        services.AddScoped<IParentChildContextBuilder, ParentChildContextBuilder>();
        services.AddScoped<ITeacherClassContextBuilder, TeacherClassContextBuilder>();
        services.AddScoped<ITeacherStudentContextBuilder, TeacherStudentContextBuilder>();
        services.AddScoped<IManagerSchoolContextBuilder, ManagerSchoolContextBuilder>();
        services.AddScoped<IManagerClassContextBuilder, ManagerClassContextBuilder>();
        services.AddScoped<IManagerStudentContextBuilder, ManagerStudentContextBuilder>();
        services.AddScoped<IAiRoleContextService, AiRoleContextService>();
        var authentication = OtpAuthenticationOptions.Load(configuration, environmentName);
        services.AddOptions<OtpAuthenticationOptions>()
            .Configure(options =>
            {
                options.Enabled = authentication.Enabled;
                options.AllowMockAuthentication = authentication.AllowMockAuthentication;
            })
            .Validate(options => OtpAuthenticationOptions.IsSafe(options, environmentName),
                "Mock Authentication به محیط تست و مجوز صریح مستقل نیاز دارد.")
            .ValidateOnStart();
        var otpBypass = DevelopmentOtpBypassOptions.Load(configuration, environmentName);
        services.AddSingleton(otpBypass);
        services.AddSingleton(provider => CreateOtpOptions(configuration, environmentName, otpBypass,
            provider.GetRequiredService<IOptions<OtpAuthenticationOptions>>().Value));
        services.AddSingleton(CreateSessionOptions(configuration));
        services.AddSingleton(CreateMessagingOptions(configuration));
        services.AddSingleton<ICommunicationEligibilityPolicy, CommunicationEligibilityPolicy>();
        services.AddSingleton<IConversationManagementPolicy, ConversationManagementAuthorizationPolicy>();
        AddContentSafetyProvider(services, configuration, environmentName);
        var storageOptions = CreateStorageOptions(configuration);
        services.AddSingleton(storageOptions);
        services.AddSingleton(TerminalFileCleanupOptions.FromConfiguration(configuration));
        services.AddSingleton<TerminalFileCleanupCursor>();
        services.AddScoped<TerminalFileCleanupProcessor>();
        services.AddHostedService<TerminalFileCleanupDispatcher>();
        services.AddSingleton(CreateReportExportOptions(configuration));
        services.AddSingleton(CreateNotificationDeliveryOptions(configuration));
        services.AddSingleton(CreatePaymentOptions(configuration));
        services.AddSingleton<FileUploadPolicy>();
        services.AddSingleton<IIdentitySecurity>(
            new IdentityCryptography(CreateSecurityKey(
                configuration,
                environmentName,
                UsesManagedOtpProvider(configuration))));
        AddOtpDeliveryProvider(services, configuration, environmentName, otpBypass, authentication);
        AddNotificationDeliveryProviders(services, configuration, environmentName);
        AddPaymentGateway(services, configuration);

        if (IsDevelopmentLike(environmentName) || !string.IsNullOrWhiteSpace(storageOptions.RootPath))
        {
            services.AddSingleton<LocalFileStorage>();
            services.AddSingleton<IFileStorage>(serviceProvider =>
                serviceProvider.GetRequiredService<LocalFileStorage>());
            services.AddSingleton<IStorageReadinessProbe>(serviceProvider =>
                serviceProvider.GetRequiredService<LocalFileStorage>());
        }
        else
        {
            services.AddSingleton<UnavailableFileStorage>();
            services.AddSingleton<IFileStorage>(serviceProvider =>
                serviceProvider.GetRequiredService<UnavailableFileStorage>());
            services.AddSingleton<IStorageReadinessProbe>(serviceProvider =>
                serviceProvider.GetRequiredService<UnavailableFileStorage>());
        }

        return services;
    }

    private static void AddContentSafetyProvider(
        IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        var defaultProvider = "Internal";
        var provider = configuration["Messaging:ContentSafetyProvider"]?.Trim() ?? defaultProvider;
        if (provider.Equals("Internal", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IContentSafetyProvider, InternalMakanContentSafetyProvider>();
            return;
        }
        if (provider.Equals("DevelopmentAllow", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsDevelopmentLike(environmentName))
            {
                throw new InvalidOperationException(
                    "Messaging:ContentSafetyProvider=DevelopmentAllow is allowed only in Development and Testing environments.");
            }

            services.AddSingleton<IContentSafetyProvider, DevelopmentAllowContentSafetyProvider>();
            return;
        }

        if (provider.Equals("Unavailable", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IContentSafetyProvider, UnavailableContentSafetyProvider>();
            return;
        }

        throw new InvalidOperationException(
            "Messaging:ContentSafetyProvider must be Internal, Unavailable or DevelopmentAllow (development/testing only).");
    }

    private static void AddPaymentGateway(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration[$"{PaymentOptions.SectionName}:Provider"]?.Trim()
            ?? "Unavailable";
        if (!provider.Equals("Unavailable", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Payments:Provider is not supported. A real provider requires an approved adapter contract.");
        }

        services.AddSingleton<IPaymentGateway, UnavailablePaymentGateway>();
    }

    private static void AddNotificationDeliveryProviders(
        IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        var defaultProvider = IsDevelopmentLike(environmentName) ? "Development" : "Unavailable";
        var pushProvider = configuration["Notifications:PushProvider"]?.Trim() ?? defaultProvider;
        if (pushProvider.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsDevelopmentLike(environmentName))
            {
                throw new InvalidOperationException(
                    "Notifications:PushProvider=Development is allowed only in Development and Testing environments.");
            }

            services.AddSingleton<IPushNotificationProvider, DevelopmentPushNotificationProvider>();
        }
        else if (pushProvider.Equals("Unavailable", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IPushNotificationProvider, UnavailablePushNotificationProvider>();
        }
        else if (pushProvider.Equals("WebPushAndFcm", StringComparison.OrdinalIgnoreCase))
        {
            services.AddMessengerPushTransports(configuration);
        }
        else
        {
            throw new InvalidOperationException(
                "Notifications:PushProvider must be Development, Unavailable or WebPushAndFcm.");
        }

        services.AddSingleton<IPushClientConfigurationProvider>(provider =>
            provider.GetRequiredService<IPushNotificationProvider>() as IPushClientConfigurationProvider ?? new UnavailablePushNotificationProvider());

        var smsProvider = configuration["Notifications:SmsProvider"]?.Trim() ?? defaultProvider;
        if (smsProvider.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsDevelopmentLike(environmentName))
            {
                throw new InvalidOperationException(
                    "Notifications:SmsProvider=Development is allowed only in Development and Testing environments.");
            }

            services.AddSingleton<INotificationSmsProvider, DevelopmentNotificationSmsProvider>();
        }
        else if (smsProvider.Equals("Unavailable", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<INotificationSmsProvider, UnavailableNotificationSmsProvider>();
        }
        else
        {
            throw new InvalidOperationException(
                "Notifications:SmsProvider must be Development or Unavailable until an approved general SMS contract exists.");
        }
    }

    private static void AddOtpDeliveryProvider(
        IServiceCollection services,
        IConfiguration configuration,
        string environmentName,
        DevelopmentOtpBypassOptions otpBypass,
        OtpAuthenticationOptions authentication)
    {
        if (!authentication.Enabled)
        {
            // مسیر Mock اصلاً delivery را فراخوانی نمی‌کند؛ ثبت adapter ناموجود دفاع در عمق است.
            services.AddSingleton<IOtpDeliveryProvider, UnavailableOtpDeliveryProvider>();
            return;
        }
        if (otpBypass.Mode == OtpMode.DevelopmentBypass)
        {
            services.AddSingleton<IOtpDeliveryProvider, DevelopmentBypassOtpProvider>();
            return;
        }
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
        string environmentName,
        DevelopmentOtpBypassOptions otpBypass,
        OtpAuthenticationOptions authentication)
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
            Enabled = authentication.Enabled,
            Mode = otpBypass.Mode,
            DevelopmentTestUsers = otpBypass.TestUsers,
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

    private static SessionOptions CreateSessionOptions(IConfiguration configuration)
    {
        var accessLifetime = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            "Identity:Session:AccessLifetimeSeconds",
            3_600));
        var refreshLifetime = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            "Identity:Session:RefreshLifetimeSeconds",
            2_592_000));
        if (refreshLifetime <= accessLifetime)
        {
            throw new InvalidOperationException(
                "Identity:Session:RefreshLifetimeSeconds must be greater than AccessLifetimeSeconds.");
        }

        return new SessionOptions
        {
            AccessLifetime = accessLifetime,
            RefreshLifetime = refreshLifetime,
            MaximumDeviceNameLength = Math.Min(200, ReadPositiveInt(
                configuration,
                "Identity:Session:MaximumDeviceNameLength",
                100))
        };
    }

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

        var minimumPeopleSearchLength = ReadPositiveInt(
            configuration,
            $"{MessagingOptions.SectionName}:MinimumPeopleSearchLength",
            2);
        var maximumPeopleSearchLength = ReadPositiveInt(
            configuration,
            $"{MessagingOptions.SectionName}:MaximumPeopleSearchLength",
            100);
        var maximumDirectoryPageSize = ReadPositiveInt(
            configuration,
            $"{MessagingOptions.SectionName}:MaximumDirectoryPageSize",
            50);
        var defaultDirectoryPageSize = ReadPositiveInt(
            configuration,
            $"{MessagingOptions.SectionName}:DefaultDirectoryPageSize",
            20);
        if (minimumPeopleSearchLength > maximumPeopleSearchLength ||
            defaultDirectoryPageSize > maximumDirectoryPageSize ||
            maximumDirectoryPageSize > 200)
        {
            throw new InvalidOperationException(
                "Messaging directory search and page-size settings are inconsistent.");
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
                50),
            MinimumPeopleSearchLength = minimumPeopleSearchLength,
            MaximumPeopleSearchLength = maximumPeopleSearchLength,
            DefaultDirectoryPageSize = defaultDirectoryPageSize,
            MaximumDirectoryPageSize = maximumDirectoryPageSize
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

    private static NotificationDeliveryOptions CreateNotificationDeliveryOptions(
        IConfiguration configuration) => new()
    {
        BatchSize = Math.Min(200, ReadPositiveInt(
            configuration,
            $"{NotificationDeliveryOptions.SectionName}:BatchSize",
            50)),
        ClaimLease = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            $"{NotificationDeliveryOptions.SectionName}:ClaimLeaseSeconds",
            30)),
        IdlePollingInterval = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            $"{NotificationDeliveryOptions.SectionName}:IdlePollingSeconds",
            2)),
        InitialRetryDelay = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            $"{NotificationDeliveryOptions.SectionName}:InitialRetryDelaySeconds",
            5)),
        MaximumRetryDelay = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            $"{NotificationDeliveryOptions.SectionName}:MaximumRetryDelaySeconds",
            300)),
        ProviderTimeout = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            $"{NotificationDeliveryOptions.SectionName}:ProviderTimeoutSeconds",
            10)),
        MaximumAttempts = Math.Min(10, ReadPositiveInt(
            configuration,
            $"{NotificationDeliveryOptions.SectionName}:MaximumAttempts",
            3))
    };

    private static PaymentOptions CreatePaymentOptions(IConfiguration configuration) => new()
    {
        InitiationClaimLease = TimeSpan.FromSeconds(ReadPositiveInt(
            configuration,
            $"{PaymentOptions.SectionName}:InitiationClaimLeaseSeconds",
            30))
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

    private static AiServiceAuthenticationOptions CreateAiServiceAuthenticationOptions(
        IConfiguration configuration) => new()
    {
        Issuer = configuration[$"{AiServiceAuthenticationOptions.SectionName}:Issuer"]?.Trim()
            ?? "makan-backend",
        Audience = configuration[$"{AiServiceAuthenticationOptions.SectionName}:Audience"]?.Trim()
            ?? "makan-ai",
        ServiceName = configuration[$"{AiServiceAuthenticationOptions.SectionName}:ServiceName"]?.Trim()
            ?? "makan-backend",
        Scope = configuration[$"{AiServiceAuthenticationOptions.SectionName}:Scope"]?.Trim()
            ?? "makan.ai.invoke",
        CurrentKeyId = configuration[$"{AiServiceAuthenticationOptions.SectionName}:CurrentKeyId"]?.Trim()
            ?? string.Empty,
        CurrentKey = configuration[$"{AiServiceAuthenticationOptions.SectionName}:CurrentKey"]
            ?? string.Empty,
        TokenLifetimeSeconds = ReadPositiveInt(
            configuration,
            $"{AiServiceAuthenticationOptions.SectionName}:TokenLifetimeSeconds",
            120)
    };

    private static AiServiceOptions CreateAiServiceOptions(IConfiguration configuration) => new()
    {
        Enabled = bool.TryParse(configuration[$"{AiServiceOptions.SectionName}:Enabled"], out var enabled) && enabled,
        BaseUrl = configuration[$"{AiServiceOptions.SectionName}:BaseUrl"]?.Trim()
            ?? new AiServiceOptions().BaseUrl,
        TimeoutSeconds = ReadPositiveInt(
            configuration,
            $"{AiServiceOptions.SectionName}:TimeoutSeconds",
            AiServiceOptions.DefaultTimeoutSeconds),
        ContractVersion = configuration[$"{AiServiceOptions.SectionName}:ContractVersion"]?.Trim()
            ?? "v1",
        CircuitBreakerFailureThreshold = ReadPositiveInt(
            configuration,
            $"{AiServiceOptions.SectionName}:CircuitBreakerFailureThreshold",
            3),
        CircuitBreakerBreakSeconds = ReadPositiveInt(
            configuration,
            $"{AiServiceOptions.SectionName}:CircuitBreakerBreakSeconds",
            30)
    };

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
