using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using MakanApp.Api.Authentication;
using MakanApp.Api.Authorization;
using MakanApp.Api.Configuration;
using MakanApp.Api.Errors;
using MakanApp.Api.Hosting;
using MakanApp.Api.Observability;
using MakanApp.Api.OpenApi;
using MakanApp.Api.Platform;
using MakanApp.Api.Realtime;
using MakanApp.Application.Ai;
using MakanApp.Application.Identity;
using MakanApp.Application.Commerce;
using MakanApp.Application.Messaging;
using MakanApp.Application.Storage;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure;
using MakanApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
ProductionLoggingSafety.Configure(builder.Services, builder.Environment);

ProductionConfigurationValidator.Validate(
    builder.Configuration,
    builder.Environment.EnvironmentName);
HostShutdownConfiguration.ConfigureServices(builder.Services, builder.Configuration);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
// تولید OpenAPI از گزینه‌های HTTP JSON استفاده می‌کند، نه صرفاً تنظیم serializer در MVC.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services
    .AddAuthentication(SessionAuthenticationDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
        SessionAuthenticationDefaults.Scheme,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in PlatformPermissionKeys.All)
    {
        options.AddPolicy(permission, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PlatformPermissionRequirement(permission));
        });
    }
});
builder.Services.AddScoped<IAuthorizationHandler, PlatformPermissionAuthorizationHandler>();
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];
    options.AddDefaultPolicy(policy =>
    {
        policy.WithExposedHeaders("Retry-After", CorrelationIdMiddleware.HeaderName);
        if (allowedOrigins.Length == 0 &&
            (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")))
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy.WithOrigins(allowedOrigins)
            .WithHeaders("authorization", "content-type", "x-client-version", CorrelationIdMiddleware.HeaderName)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var isAdminMutation = httpContext.Request.Path.StartsWithSegments(
                                  "/api/admin/v1",
                                  StringComparison.OrdinalIgnoreCase) &&
                              !HttpMethods.IsGet(httpContext.Request.Method) &&
                              !HttpMethods.IsHead(httpContext.Request.Method) &&
                              !HttpMethods.IsOptions(httpContext.Request.Method);
        return isAdminMutation
            ? RateLimitPartition.GetFixedWindowLimiter(
                GetAuthenticatedPartitionKey(httpContext),
                _ => FixedWindow(builder.Environment, 30))
            : RateLimitPartition.GetNoLimiter("unlimited");
    });
    options.AddPolicy("otp", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetIpPartitionKey(httpContext),
            _ => FixedWindow(builder.Environment, 10)));
    options.AddPolicy("otp-verify", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetIpPartitionKey(httpContext),
            _ => FixedWindow(builder.Environment, 20)));
    options.AddPolicy("session-refresh", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetIpPartitionKey(httpContext),
            _ => FixedWindow(builder.Environment, 30)));
    options.AddPolicy("payment", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetAuthenticatedPartitionKey(httpContext),
            _ => FixedWindow(builder.Environment, 20)));
    options.AddPolicy("messaging-discovery", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetAuthenticatedPartitionKey(httpContext),
            _ => FixedWindow(builder.Environment, 60)));
    options.AddPolicy("messaging-search", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetAuthenticatedPartitionKey(httpContext),
            _ => FixedWindow(builder.Environment, 60)));
    options.AddPolicy("notification-device", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetAuthenticatedPartitionKey(httpContext),
            _ => FixedWindow(builder.Environment, 60)));
    options.AddPolicy("messaging-send", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(GetAuthenticatedPartitionKey(httpContext), _ => FixedWindow(builder.Environment, 300)));
    options.AddPolicy("messaging-reaction", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(GetAuthenticatedPartitionKey(httpContext), _ => FixedWindow(builder.Environment, 300)));
    options.AddPolicy("messaging-forward", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(GetAuthenticatedPartitionKey(httpContext), _ => FixedWindow(builder.Environment, 120)));
    options.AddPolicy("file-upload", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetAuthenticatedPartitionKey(httpContext),
            _ => FixedWindow(builder.Environment, 20)));
    options.AddPolicy("report-export", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetAuthenticatedPartitionKey(httpContext),
            _ => FixedWindow(builder.Environment, 10)));
    options.AddPolicy("ai-chat", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetAuthenticatedPartitionKey(httpContext),
            _ => FixedWindow(builder.Environment, 20)));
    options.OnRejected = async (context, cancellationToken) =>
    {
        var isPayment = context.HttpContext.Request.Path.StartsWithSegments(
            "/api/v1/commerce/orders",
            StringComparison.OrdinalIgnoreCase);
        var isMessagingDiscovery = context.HttpContext.Request.Path.StartsWithSegments(
            "/api/v1/people",
            StringComparison.OrdinalIgnoreCase) ||
            context.HttpContext.Request.Path.StartsWithSegments(
                "/api/v1/messages/search",
                StringComparison.OrdinalIgnoreCase);
        var isOtp = context.HttpContext.Request.Path.StartsWithSegments(
            "/api/v1/auth/otp",
            StringComparison.OrdinalIgnoreCase);
        var isNotificationDevice = context.HttpContext.Request.Path.StartsWithSegments("/api/v1/notifications/devices", StringComparison.OrdinalIgnoreCase);
        var isMessagingWrite = context.HttpContext.Request.Path.StartsWithSegments("/api/v1/conversations", StringComparison.OrdinalIgnoreCase) &&
            !HttpMethods.IsGet(context.HttpContext.Request.Method) && !HttpMethods.IsHead(context.HttpContext.Request.Method);
        var isFileUpload = context.HttpContext.Request.Path.StartsWithSegments("/api/v1/files", StringComparison.OrdinalIgnoreCase) &&
            HttpMethods.IsPost(context.HttpContext.Request.Method);
        if ((isMessagingDiscovery || isNotificationDevice || isMessagingWrite || isFileUpload) && context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        var isAi = context.HttpContext.Request.Path.StartsWithSegments(
            "/api/v1/ai",
            StringComparison.OrdinalIgnoreCase);
        var problemDetailsService = context.HttpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = isAi
                    ? "تعداد درخواست‌های هوش مصنوعی بیش از حد مجاز است."
                    : isPayment
                    ? "تعداد درخواست‌های پرداخت بیش از حد مجاز است."
                    : isMessagingDiscovery
                        ? "تعداد درخواست‌های جست‌وجوی کاربر بیش از حد مجاز است."
                        : isOtp
                            ? "تعداد درخواست‌های کد بیش از حد مجاز است."
                            : "تعداد درخواست‌ها بیش از حد مجاز است.",
                Type = isAi
                    ? "urn:makan:problem:ai-rate-limited"
                    : isPayment
                    ? "urn:makan:problem:payment-rate-limited"
                    : isMessagingDiscovery
                        ? "urn:makan:problem:messaging-discovery-rate-limited"
                        : isOtp
                            ? "urn:makan:problem:otp-rate-limited"
                            : "urn:makan:problem:rate-limited",
                Extensions =
                {
                    ["code"] = isAi
                        ? AiServiceErrorCodes.RateLimited
                        : isPayment
                        ? CommerceErrorCodes.PaymentRateLimited
                        : isMessagingDiscovery
                            ? MessagingErrorCodes.DiscoveryRateLimited
                            : isOtp
                                ? IdentityErrorCodes.OtpRateLimited
                                : ApiErrorCodes.RateLimited
                }
            }
        });
    };
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = 1;
    options.KnownProxies.Clear();
    foreach (var value in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    {
        if (IPAddress.TryParse(value, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }
});
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(builder.Configuration.GetValue("Hosting:HstsDays", 365));
});
builder.Services.AddOpenApi(options => options.AddSchemaTransformer<StringEnumSchemaTransformer>());
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        context.ProblemDetails.Extensions.TryAdd("correlationId", context.HttpContext.TraceIdentifier);
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddMetrics();
builder.Services.AddSingleton<MakanApiMetrics>();
builder.Services.AddHealthChecks()
    .AddCheck<CoreReadinessHealthCheck>("core-readiness", tags: ["ready"], timeout: TimeSpan.FromSeconds(15));
builder.Services.AddInfrastructure(
    builder.Configuration,
    builder.Environment.EnvironmentName);
builder.Services.AddScoped<SignalRMessagingRealtimeNotifier>();
builder.Services.AddScoped<SignalRMessagingEphemeralNotifier>();
builder.Services.AddScoped<IMessagingEphemeralNotifier>(provider => provider.GetRequiredService<SignalRMessagingEphemeralNotifier>());
builder.Services.AddScoped<IMessagingRealtimeNotifier>(serviceProvider =>
    serviceProvider.GetRequiredService<SignalRMessagingRealtimeNotifier>());
builder.Services.AddOptions<FormOptions>().Configure<StorageOptions>((formOptions, storageOptions) =>
{
    formOptions.MultipartBodyLengthLimit = checked(storageOptions.MaxFileSizeBytes + 65_536);
});

var app = builder.Build();

var otpAuthentication = app.Services.GetRequiredService<IOptions<OtpAuthenticationOptions>>().Value;
if (!otpAuthentication.Enabled)
{
    app.Logger.LogWarning(new EventId(1010, "MockOtpAuthenticationEnabled"),
        "احراز هویت آزمایشی فعال است؛ فقط حساب و دادهٔ تست مجاز است. Environment={Environment}; OtpEnabled={OtpEnabled}; AllowMockAuthentication={AllowMockAuthentication}",
        app.Environment.EnvironmentName, otpAuthentication.Enabled, otpAuthentication.AllowMockAuthentication);
}
else
{
    app.Logger.LogInformation(new EventId(1011, "RealOtpAuthenticationEnabled"),
        "احراز هویت OTP واقعی فعال است. Environment={Environment}; OtpEnabled={OtpEnabled}; AllowMockAuthentication={AllowMockAuthentication}",
        app.Environment.EnvironmentName, otpAuthentication.Enabled, otpAuthentication.AllowMockAuthentication);
}

if (app.Environment.IsProduction())
{
    if (app.Configuration.GetValue("Hosting:AllowInsecureHttp", false))
    {
        app.Logger.LogWarning(
            "استثنای Hosting:AllowInsecureHttp فعال است؛ ارتباط HTTP، OTP و توکن‌های ورود را در مسیر شبکه رمزگذاری نمی‌کند.");
    }

    if (app.Configuration.GetValue("Database:AllowUntrustedServerCertificate", false))
    {
        app.Logger.LogWarning(
            "استثنای Database:AllowUntrustedServerCertificate فعال است؛ اتصال SQL با TrustServerCertificate=True هویت گواهی سرور را بررسی نمی‌کند.");
    }
}

if (await PlatformOwnerBootstrapRunner.TryRunAsync(app, args))
{
    return;
}

if (builder.Configuration.GetValue("ReverseProxy:Enabled", false))
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<HttpRequestMetricsMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();

var exposeOpenApi = !(app.Environment.IsProduction() || app.Environment.IsStaging()) ||
                    builder.Configuration.GetValue("Swagger:Enabled", false);
if (exposeOpenApi)
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("../openapi/v1.json", "MakanApp API v1");
    });
}

if (app.Environment.IsProduction() && builder.Configuration.GetValue("Hosting:RequireHttps", true))
{
    app.UseHsts();
}

if (!app.Environment.IsEnvironment("Testing") &&
    builder.Configuration.GetValue("Hosting:RequireHttps", true))
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous().WithMetadata(new CoreHealthProbeMetadata());
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous().WithMetadata(new CoreHealthProbeMetadata());
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous().WithMetadata(new CoreHealthProbeMetadata());
app.MapControllers();
app.MapHub<MessagingHub>("/hubs/messaging");

app.Run();

static string GetIpPartitionKey(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static string GetAuthenticatedPartitionKey(HttpContext context) =>
    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? GetIpPartitionKey(context);

static FixedWindowRateLimiterOptions FixedWindow(IHostEnvironment environment, int permitLimit) => new()
{
    PermitLimit = environment.IsEnvironment("Testing") ? 10_000 : permitLimit,
    Window = TimeSpan.FromMinutes(1),
    QueueLimit = 0,
    AutoReplenishment = true
};

public partial class Program;
