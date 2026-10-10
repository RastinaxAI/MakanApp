using System.Net;
using MakanApp.Application.Identity;
using MakanApp.Infrastructure;
using MakanApp.Infrastructure.Identity.Sms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class MeliPayamakSmsSenderTests
{
    private const string Username = "sms-user";
    private const string Credential = "credential&=secret";
    private const string SenderNumber = "500012345";
    private const string OtpCode = "481527";
    private const string CanonicalPhoneNumber = "+989121234567";

    [Fact]
    public async Task SendOtpPostsDocumentedFormToCorrectEndpointAndMapsRecId()
    {
        HttpMethod? method = null;
        Uri? requestUri = null;
        string? mediaType = null;
        string? body = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            method = request.Method;
            requestUri = request.RequestUri;
            mediaType = request.Content?.Headers.ContentType?.MediaType;
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Response(HttpStatusCode.OK, "987654321");
        });
        var logger = new RecordingLogger<MeliPayamakSmsSender>();
        var sender = CreateSender(handler, logger);

        await sender.SendOtpAsync(
            CanonicalPhoneNumber,
            Guid.NewGuid(),
            OtpCode,
            DateTime.UtcNow.AddMinutes(5),
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal(
            "https://rest.payamak-panel.com/api/SendSMS/SendOtp",
            requestUri?.AbsoluteUri);
        Assert.Equal("application/x-www-form-urlencoded", mediaType);
        var fields = ParseForm(body!);
        Assert.Equal(Username, fields["username"]);
        Assert.Equal(Credential, fields["password"]);
        Assert.Equal(SenderNumber, fields["from"]);
        Assert.Equal("09121234567", fields["to"]);
        Assert.Equal(OtpCode, fields["code"]);
        Assert.Contains(logger.Messages, message => message.Contains("987654321", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("-111", IdentityErrorCodes.SmsProviderConfigurationError)]
    [InlineData("-110", IdentityErrorCodes.SmsProviderConfigurationError)]
    [InlineData("-109", IdentityErrorCodes.SmsProviderConfigurationError)]
    [InlineData("-108", IdentityErrorCodes.SmsProviderAuthenticationFailed)]
    [InlineData("0", IdentityErrorCodes.SmsProviderAuthenticationFailed)]
    [InlineData("2", IdentityErrorCodes.SmsProviderRejected)]
    [InlineData("3", IdentityErrorCodes.SmsRateLimited)]
    [InlineData("4", IdentityErrorCodes.SmsRateLimited)]
    [InlineData("5", IdentityErrorCodes.SmsProviderConfigurationError)]
    [InlineData("6", IdentityErrorCodes.SmsProviderUnavailable)]
    [InlineData("7", IdentityErrorCodes.SmsProviderRejected)]
    [InlineData("9", IdentityErrorCodes.SmsProviderConfigurationError)]
    [InlineData("10", IdentityErrorCodes.SmsProviderAuthenticationFailed)]
    [InlineData("11", IdentityErrorCodes.SmsProviderRejected)]
    [InlineData("12", IdentityErrorCodes.SmsProviderConfigurationError)]
    [InlineData("14", IdentityErrorCodes.SmsProviderRejected)]
    [InlineData("15", IdentityErrorCodes.SmsProviderRejected)]
    [InlineData("16", IdentityErrorCodes.SmsRecipientInvalid)]
    [InlineData("17", IdentityErrorCodes.SmsProviderRejected)]
    [InlineData("18", IdentityErrorCodes.SmsRecipientInvalid)]
    [InlineData("35", IdentityErrorCodes.SmsRecipientBlocked)]
    public async Task DocumentedProviderErrorMapsToSafeInternalCategory(
        string providerResponse,
        string expectedErrorCode)
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(Response(HttpStatusCode.OK, providerResponse)));
        var sender = CreateSender(handler, new RecordingLogger<MeliPayamakSmsSender>());

        var exception = await Assert.ThrowsAsync<IdentityException>(() => sender.SendOtpAsync(
            CanonicalPhoneNumber,
            Guid.NewGuid(),
            OtpCode,
            DateTime.UtcNow.AddMinutes(5),
            CancellationToken.None));

        Assert.Equal(expectedErrorCode, exception.Code);
        Assert.DoesNotContain(providerResponse, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidProviderResponseFailsSafely()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(Response(HttpStatusCode.OK, "not-a-provider-response")));
        var sender = CreateSender(handler, new RecordingLogger<MeliPayamakSmsSender>());

        var exception = await Assert.ThrowsAsync<IdentityException>(() => sender.SendOtpAsync(
            CanonicalPhoneNumber,
            Guid.NewGuid(),
            OtpCode,
            DateTime.UtcNow.AddMinutes(5),
            CancellationToken.None));

        Assert.Equal(IdentityErrorCodes.SmsProviderUnavailable, exception.Code);
        Assert.DoesNotContain("not-a-provider-response", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HttpFailureFailsSafely()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(Response(HttpStatusCode.BadGateway, "provider-details")));
        var sender = CreateSender(handler, new RecordingLogger<MeliPayamakSmsSender>());

        var exception = await Assert.ThrowsAsync<IdentityException>(() => sender.SendOtpAsync(
            CanonicalPhoneNumber,
            Guid.NewGuid(),
            OtpCode,
            DateTime.UtcNow.AddMinutes(5),
            CancellationToken.None));

        Assert.Equal(IdentityErrorCodes.SmsProviderUnavailable, exception.Code);
        Assert.DoesNotContain("provider-details", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TimeoutMapsToUnavailableWithoutRetry()
    {
        var callCount = 0;
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response(HttpStatusCode.OK, "987654321");
        });
        var logger = new RecordingLogger<MeliPayamakSmsSender>();
        var sender = CreateSender(handler, logger, TimeSpan.FromMilliseconds(50));

        var exception = await Assert.ThrowsAsync<IdentityException>(() => sender.SendOtpAsync(
            CanonicalPhoneNumber,
            Guid.NewGuid(),
            OtpCode,
            DateTime.UtcNow.AddMinutes(5),
            CancellationToken.None));

        Assert.Equal(IdentityErrorCodes.SmsProviderUnavailable, exception.Code);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task CallerCancellationIsPreserved()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response(HttpStatusCode.OK, "987654321");
        });
        var sender = CreateSender(handler, new RecordingLogger<MeliPayamakSmsSender>());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.SendOtpAsync(
            CanonicalPhoneNumber,
            Guid.NewGuid(),
            OtpCode,
            DateTime.UtcNow.AddMinutes(5),
            cancellationSource.Token));
    }

    [Fact]
    public async Task LogsContainNoCredentialsOtpOrFullPhoneNumber()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(Response(HttpStatusCode.OK, "987654321")));
        var logger = new RecordingLogger<MeliPayamakSmsSender>();
        var sender = CreateSender(handler, logger);

        await sender.SendOtpAsync(
            CanonicalPhoneNumber,
            Guid.NewGuid(),
            OtpCode,
            DateTime.UtcNow.AddMinutes(5),
            CancellationToken.None);

        var logs = string.Join(Environment.NewLine, logger.Messages);
        Assert.DoesNotContain(Username, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(Credential, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(OtpCode, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(CanonicalPhoneNumber, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("09121234567", logs, StringComparison.Ordinal);
        Assert.Contains("4567", logs, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionsRequireSecretsAndSecureBaseUrlWhenProviderIsSelected()
    {
        var missingSecrets = new MeliPayamakOptions();
        var insecureBaseUrl = new MeliPayamakOptions
        {
            BaseUrl = "http://rest.payamak-panel.com",
            Username = Username,
            Credential = Credential,
            From = SenderNumber,
            TimeoutSeconds = 10
        };

        Assert.Throws<InvalidOperationException>(missingSecrets.ValidateAndGetBaseUri);
        Assert.Throws<InvalidOperationException>(insecureBaseUrl.ValidateAndGetBaseUri);
    }

    [Fact]
    public void SelectedMeliPayamakProviderValidatesSecretsDuringRegistration()
    {
        var configuration = BaseConfiguration();
        configuration["Sms:Provider"] = "MeliPayamak";
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration, "Production"));

        Assert.Contains("Sms:MeliPayamak", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentProviderDoesNotRequireMeliPayamakSecrets()
    {
        var configuration = BaseConfiguration();
        configuration["Sms:Provider"] = "Development";
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration, "Development");

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ISmsSender));
    }

    private static MeliPayamakSmsSender CreateSender(
        HttpMessageHandler handler,
        RecordingLogger<MeliPayamakSmsSender> logger,
        TimeSpan? httpTimeout = null)
    {
        var options = ValidOptions();
        var client = new HttpClient(handler)
        {
            BaseAddress = options.ValidateAndGetBaseUri(),
            Timeout = httpTimeout ?? TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
        return new MeliPayamakSmsSender(client, options, logger);
    }

    private static MeliPayamakOptions ValidOptions() => new()
    {
        BaseUrl = MeliPayamakOptions.DefaultBaseUrl,
        Username = Username,
        Credential = Credential,
        From = SenderNumber,
        TimeoutSeconds = 10
    };

    private static ConfigurationManager BaseConfiguration()
    {
        var configuration = new ConfigurationManager
        {
            ["ConnectionStrings:MakanDatabase"] =
                "Server=(localdb)\\MSSQLLocalDB;Database=SmsRegistrationOnly;Trusted_Connection=True;",
            ["Identity:SecurityKey"] = new string('S', 32)
        };
        return configuration;
    }

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string content) =>
        new(statusCode) { Content = new StringContent(content) };

    private static IReadOnlyDictionary<string, string> ParseForm(string body) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(field => field.Split('=', 2))
            .ToDictionary(
                field => Decode(field[0]),
                field => Decode(field[1]),
                StringComparer.Ordinal);

    private static string Decode(string value) =>
        Uri.UnescapeDataString(value.Replace('+', ' '));

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
