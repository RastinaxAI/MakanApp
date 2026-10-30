using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Ai;
using MakanApp.Infrastructure.Ai;
using MakanApp.IntegrationTests;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.AiIntegrationProbe;

internal static class LiveAiProgram
{
    internal static TextWriter EvidenceOutput { get; private set; } = TextWriter.Null;

    public static async Task<int> Main(string[] args)
    {
        EvidenceOutput = Console.Out;
        if (args.SequenceEqual(["--verify-failure-sanitization"]))
        {
            FailureEvidenceCapture.VerifySanitization();
            Console.WriteLine("{\"stage\":\"failure_sanitization_self_check\",\"passed\":true}");
            return 0;
        }
        var allowRemoteHttps = args.Count(value => value == "--allow-remote-https") == 1;
        var allowWireGuard = args.Count(value => value == "--allow-wireguard") == 1;
        var selectedArgs = args.Where(value => value is not ("--allow-remote-https" or "--allow-wireguard")).ToArray();
        var evidenceOnly = selectedArgs.SequenceEqual(["--run-live", "--collect-failure-evidence"]);
        var authorizationOnly = selectedArgs.SequenceEqual(["--run-live", "--authorization-only"]);
        var serviceAuthOnly = selectedArgs.SequenceEqual(["--run-live", "--service-auth-only"]);
        var operation = selectedArgs.Length == 3 && selectedArgs[0] == "--run-live" &&
            selectedArgs[1] == "--operation" ? selectedArgs[2] : null;
        if (args.Count(value => value == "--allow-remote-https") > 1 || args.Count(value => value == "--allow-wireguard") > 1 ||
            allowRemoteHttps && allowWireGuard ||
            (!selectedArgs.SequenceEqual(["--run-live"]) && !authorizationOnly && !serviceAuthOnly && !evidenceOnly &&
             operation is not ("focused_three" or "parent_child_summary" or "remaining_roles" or
                 "remaining_managers" or "manager_student_insight")))
        {
            Console.WriteLine("اجرای واقعی فقط با --run-live و انتخاب صریح محیط مجاز است.");
            return 2;
        }

        var stage = "configuration";
        var originalOutput = Console.Out;
        var originalError = Console.Error;
        // stdout/stderr میزبان ممکن است دادهٔ fixture داشته باشد؛ فقط رویدادهای صریح منتشر می‌شوند.
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets(typeof(MakanApp.Api.Controllers.AiController).Assembly, optional: true)
                .AddEnvironmentVariables()
                .Build();
            var target = LiveAiTargetPolicy.Validate(configuration["AiService:BaseUrl"], allowRemoteHttps,
                configuration["AiLiveProbe:ApprovedOrigin"], allowWireGuard,
                allowWireGuard && System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .SelectMany(i => i.GetIPProperties().UnicastAddresses).Any(a => a.Address.ToString() == "10.77.0.1"));
            var authentication = configuration.GetSection(AiServiceAuthenticationOptions.SectionName)
                .Get<AiServiceAuthenticationOptions>() ?? new();
            var credentials = new HmacAiServiceCredentialProvider(authentication, TimeProvider.System);
            _ = await credentials.GetCredentialAsync(CancellationToken.None);
            var options = new AiServiceOptions
            {
                Enabled = true,
                BaseUrl = target.AbsoluteUri,
                TimeoutSeconds = configuration.GetValue("AiService:TimeoutSeconds", AiServiceOptions.DefaultTimeoutSeconds)
            };
            if (options.TimeoutSeconds is < 1 or > AiServiceOptions.MaximumTimeoutSeconds)
                throw new InvalidOperationException("LIVE_AI_TIMEOUT_INVALID");
            EvidenceOutput.WriteLine(JsonSerializer.Serialize(new
            {
                stage = "configuration", passed = true, AI_SERVICE_AUTH_CONFIGURED = true,
                baseUrl = target.GetLeftPart(UriPartial.Authority), options.TimeoutSeconds,
                contractVersion = options.ContractVersion
            }));
            if (serviceAuthOnly)
            {
                stage = "service_auth_real_matrix";
                await ServiceAuthProbe.RunAsync(target, credentials, authentication);
                return 0;
            }

            using var factory = new MakanAppWebApplicationFactory();
            using var observation = new LiveAiObservation();
            observation.Forbid(authentication.CurrentKey);
            var evidence = evidenceOnly ? new FailureEvidenceCapture(observation.IsSafe) : null;
            if (evidenceOnly) FailureEvidenceCapture.VerifySanitization();
            using var application = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiServiceClient>();
                services.RemoveAll<IAiServiceCredentialProvider>();
                services.RemoveAll<AiServiceOptions>();
                services.RemoveAll<AiServiceCircuitBreaker>();
                services.AddSingleton(options);
                services.AddSingleton<IAiServiceCredentialProvider>(credentials);
                services.AddSingleton<AiServiceCircuitBreaker>();
                services.AddSingleton(observation);
                if (evidence is not null) services.AddSingleton(evidence);
                services.AddTransient<LiveAiObservationHandler>();
                services.AddHttpClient<IAiServiceClient, AiServiceClient>(client =>
                        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds))
                    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
                    .AddHttpMessageHandler<LiveAiObservationHandler>();
                services.RemoveAll<ILoggerProvider>();
                services.AddSingleton<ILoggerProvider, RecordingTestLoggerProvider>();
                if (evidence is not null)
                {
                    services.RemoveAll<ILoggerProvider>();
                    services.AddSingleton<ILoggerProvider, FailureEvidenceLoggerProvider>();
                }
            }));
            stage = "isolated_database";
            await ((IAsyncLifetime)factory).InitializeAsync();
            try
            {
                stage = "real_role_and_authorization_matrix";
                var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter() }
                };
                await new LiveAiScenario(factory, application, observation, jsonOptions, options.TimeoutSeconds, evidence)
                    .RunAsync(evidenceOnly ? "failure_evidence" : operation, authorizationOnly);
            }
            finally
            {
                await ((IAsyncLifetime)factory).DisposeAsync();
            }
            if (evidence is not null) originalOutput.WriteLine(evidence.Export());
            return 0;
        }
        catch (Exception exception)
        {
            // Message و InnerException ممکن است credential یا پاسخ provider داشته باشند.
            originalOutput.WriteLine(JsonSerializer.Serialize(new { stage, passed = false, errorType = exception.GetType().Name }));
            return 1;
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }
}
