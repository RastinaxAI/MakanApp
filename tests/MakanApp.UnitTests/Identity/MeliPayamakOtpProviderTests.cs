using System.Net;
using System.Text.Json;
using MakanApp.Application.Identity;
using MakanApp.Infrastructure;
using MakanApp.Infrastructure.Identity.Otp.MeliPayamak;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class MeliPayamakOtpProviderTests
{
    private const string ServiceToken = "service-token.secret_123";
    private const string ProviderCode = "opaque-code-A7";
    private const string CanonicalPhoneNumber = "+989121234567";

    [Fact]
    public async Task RequestOtpPostsOnlyRecipientAsJsonAndParsesResponse()
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
            return Response(HttpStatusCode.OK, "{\"code\":\"opaque-code-A7\",\"status\":\"sent\"}");
        });
        var logger = new RecordingLogger<MeliPayamakOtpProvider>();
        var provider = CreateProvider(handler, logger);

        var result = await provider.RequestOtpAsync(
            CanonicalPhoneNumber,
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("console.melipayamak.com", requestUri?.Host);
        Assert.Equal($"/api/send/otp/{ServiceToken}", requestUri?.AbsolutePath);
        Assert.Equal("application/json", mediaType);
        using var document = JsonDocument.Parse(body!);
        var property = Assert.Single(document.RootElement.EnumerateObject());
        Assert.Equal("to", property.Name);
        Assert.Equal("09121234567", property.Value.GetString());
        Assert.DoesNotContain("username", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("from", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("code", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ProviderCode, result.VerificationCode);
        Assert.Equal("sent", result.ProviderStatus);
        AssertSafeLogs(logger.Messages);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"code\":null,\"status\":\"failed\"}")]
    [InlineData("{\"code\":\"  \",\"status\":\"failed\"}")]
    [InlineData("{\"code\":123456,\"status\":\"sent\"}")]
    public async Task MissingOrNonStringCodeFailsSafely(string providerResponse)
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(Response(HttpStatusCode.OK, providerResponse)));
        var logger = new RecordingLogger<MeliPayamakOtpProvider>();
        var provider = CreateProvider(handler, logger);

        var exception = await Assert.ThrowsAsync<IdentityException>(() =>
            provider.RequestOtpAsync(CanonicalPhoneNumber, CancellationToken.None));

        Assert.Equal(IdentityErrorCodes.SmsProviderUnavailable, exception.Code);
        Assert.DoesNotContain(providerResponse, exception.Message, StringComparison.Ordinal);
        AssertSafeLogs(logger.Messages);
    }

    [Fact]
    public async Task MalformedJsonFailsSafely()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(Response(HttpStatusCode.OK, "not-json")));
        var logger = new RecordingLogger<MeliPayamakOtpProvider>();
        var provider = CreateProvider(handler, logger);

        var exception = await Assert.ThrowsAsync<IdentityException>(() =>
            provider.RequestOtpAsync(CanonicalPhoneNumber, CancellationToken.None));

        Assert.Equal(IdentityErrorCodes.SmsProviderUnavailable, exception.Code);
        AssertSafeLogs(logger.Messages);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, IdentityErrorCodes.SmsProviderAuthenticationFailed)]
    [InlineData(HttpStatusCode.Forbidden, IdentityErrorCodes.SmsProviderAuthenticationFailed)]
    [InlineData(HttpStatusCode.BadRequest, IdentityErrorCodes.SmsProviderRejected)]
    [InlineData(HttpStatusCode.TooManyRequests, IdentityErrorCodes.SmsRateLimited)]
    [InlineData(HttpStatusCode.BadGateway, IdentityErrorCodes.SmsProviderUnavailable)]
    public async Task NonSuccessHttpStatusMapsToSafeFailure(
        HttpStatusCode statusCode,
        string expectedErrorCode)
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(Response(statusCode, $"provider-{ServiceToken}-{ProviderCode}")));
        var logger = new RecordingLogger<MeliPayamakOtpProvider>();
        var provider = CreateProvider(handler, logger);

        var exception = await Assert.ThrowsAsync<IdentityException>(() =>
            provider.RequestOtpAsync(CanonicalPhoneNumber, CancellationToken.None));

        Assert.Equal(expectedErrorCode, exception.Code);
        Assert.DoesNotContain(ServiceToken, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ProviderCode, exception.Message, StringComparison.Ordinal);
        AssertSafeLogs(logger.Messages);
    }

    [Fact]
    public async Task TimeoutFailsSafelyWithoutRetry()
    {
        var callCount = 0;
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response(HttpStatusCode.OK, "{\"code\":\"never\"}");
        });
        var provider = CreateProvider(
            handler,
            new RecordingLogger<MeliPayamakOtpProvider>(),
            TimeSpan.FromMilliseconds(50));

        var exception = await Assert.ThrowsAsync<IdentityException>(() =>
            provider.RequestOtpAsync(CanonicalPhoneNumber, CancellationToken.None));

        Assert.Equal(IdentityErrorCodes.SmsProviderUnavailable, exception.Code);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task CallerCancellationIsPreserved()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response(HttpStatusCode.OK, "{\"code\":\"never\"}");
        });
        var provider = CreateProvider(
            handler,
            new RecordingLogger<MeliPayamakOtpProvider>());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.RequestOtpAsync(CanonicalPhoneNumber, cancellationSource.Token));
    }

    [Fact]
    public async Task TransportExceptionDoesNotExposeSecretUrlData()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException($"transport {ServiceToken} {ProviderCode}"));
        var logger = new RecordingLogger<MeliPayamakOtpProvider>();
        var provider = CreateProvider(handler, logger);

        var exception = await Assert.ThrowsAsync<IdentityException>(() =>
            provider.RequestOtpAsync(CanonicalPhoneNumber, CancellationToken.None));

        Assert.DoesNotContain(ServiceToken, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ProviderCode, exception.Message, StringComparison.Ordinal);
        AssertSafeLogs(logger.Messages);
    }

    [Fact]
    public async Task NonStringStatusIsIgnoredWithoutRejectingValidCode()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Response(
            HttpStatusCode.OK,
            "{\"code\":\"opaque-code-A7\",\"status\":{\"detail\":\"not-safe\"}}")));
        var provider = CreateProvider(
            handler,
            new RecordingLogger<MeliPayamakOtpProvider>());

        var result = await provider.RequestOtpAsync(
            CanonicalPhoneNumber,
            CancellationToken.None);

        Assert.Equal(ProviderCode, result.VerificationCode);
        Assert.Null(result.ProviderStatus);
    }

    [Fact]
    public void OptionsRequireServiceTokenAndSecureOrigin()
    {
        var missingToken = new MeliPayamakOtpOptions();
        var insecureOrigin = new MeliPayamakOtpOptions
        {
            BaseUrl = "http://console.melipayamak.com",
            ServiceToken = ServiceToken
        };
        var originWithPath = new MeliPayamakOtpOptions
        {
            BaseUrl = "https://console.melipayamak.com/api",
            ServiceToken = ServiceToken
        };

        Assert.Throws<InvalidOperationException>(missingToken.ValidateAndGetBaseUri);
        Assert.Throws<InvalidOperationException>(insecureOrigin.ValidateAndGetBaseUri);
        Assert.Throws<InvalidOperationException>(originWithPath.ValidateAndGetBaseUri);
    }

    [Fact]
    public void SelectedMeliPayamakOtpProviderValidatesSecretDuringRegistration()
    {
        var configuration = BaseConfiguration();
        configuration["Sms:Provider"] = "MeliPayamakOtp";
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration, "Production"));

        Assert.Contains("Sms:MeliPayamakOtp:ServiceToken", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ServiceToken, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentProviderDoesNotRequireMeliPayamakOtpSecret()
    {
        var configuration = BaseConfiguration();
        configuration["Sms:Provider"] = "Development";
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration, "Development");

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IOtpDeliveryProvider));
    }

    private static MeliPayamakOtpProvider CreateProvider(
        HttpMessageHandler handler,
        RecordingLogger<MeliPayamakOtpProvider> logger,
        TimeSpan? httpTimeout = null)
    {
        var options = ValidOptions();
        var client = new HttpClient(handler)
        {
            BaseAddress = options.ValidateAndGetBaseUri(),
            Timeout = httpTimeout ?? TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
        return new MeliPayamakOtpProvider(client, options, logger);
    }

    private static MeliPayamakOtpOptions ValidOptions() => new()
    {
        BaseUrl = MeliPayamakOtpOptions.DefaultBaseUrl,
        ServiceToken = ServiceToken,
        TimeoutSeconds = 10
    };

    private static ConfigurationManager BaseConfiguration()
    {
        var configuration = new ConfigurationManager
        {
            ["ConnectionStrings:MakanDatabase"] =
                "Server=(localdb)\\MSSQLLocalDB;Database=OtpRegistrationOnly;Trusted_Connection=True;",
            ["Identity:SecurityKey"] = new string('S', 32)
        };
        return configuration;
    }

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string content) =>
        new(statusCode) { Content = new StringContent(content) };

    private static void AssertSafeLogs(IEnumerable<string> messages)
    {
        var logs = string.Join(Environment.NewLine, messages);
        Assert.DoesNotContain(ServiceToken, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(ProviderCode, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(CanonicalPhoneNumber, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("09121234567", logs, StringComparison.Ordinal);
    }

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
