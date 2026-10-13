using System.Diagnostics;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using MakanApp.Api.Authentication;
using MakanApp.Api.Authorization;
using MakanApp.Api.Errors;
using MakanApp.Api.Platform;
using MakanApp.Api.Realtime;
using MakanApp.Application.Identity;
using MakanApp.Application.Commerce;
using MakanApp.Application.Messaging;
using MakanApp.Application.Storage;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
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
    options.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("otp", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("payment", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("messaging-discovery", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        var isPayment = context.HttpContext.Request.Path.StartsWithSegments(
            "/api/v1/commerce/orders",
            StringComparison.OrdinalIgnoreCase);
        var isMessagingDiscovery = context.HttpContext.Request.Path.StartsWithSegments(
            "/api/v1/people",
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
                Detail = isPayment
                    ? "تعداد درخواست‌های پرداخت بیش از حد مجاز است."
                    : isMessagingDiscovery
                        ? "تعداد درخواست‌های جست‌وجوی کاربر بیش از حد مجاز است."
                        : "تعداد درخواست‌های کد بیش از حد مجاز است.",
                Type = isPayment
                    ? "urn:makan:problem:payment-rate-limited"
                    : isMessagingDiscovery
                        ? "urn:makan:problem:messaging-discovery-rate-limited"
                        : "urn:makan:problem:otp-rate-limited",
                Extensions =
                {
                    ["code"] = isPayment
                        ? CommerceErrorCodes.PaymentRateLimited
                        : isMessagingDiscovery
                            ? MessagingErrorCodes.DiscoveryRateLimited
                            : IdentityErrorCodes.OtpRateLimited
                }
            }
        });
    };
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(
    builder.Configuration,
    builder.Environment.EnvironmentName);
builder.Services.AddScoped<SignalRMessagingRealtimeNotifier>();
builder.Services.AddScoped<IMessagingRealtimeNotifier>(serviceProvider =>
    serviceProvider.GetRequiredService<SignalRMessagingRealtimeNotifier>());
builder.Services.AddOptions<FormOptions>().Configure<StorageOptions>((formOptions, storageOptions) =>
{
    formOptions.MultipartBodyLengthLimit = checked(storageOptions.MaxFileSizeBytes + 65_536);
});

var app = builder.Build();

if (await PlatformOwnerBootstrapRunner.TryRunAsync(app, args))
{
    return;
}

app.UseExceptionHandler();

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("../openapi/v1.json", "MakanApp API v1");
});

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<MessagingHub>("/hubs/messaging");

app.Run();

public partial class Program;
