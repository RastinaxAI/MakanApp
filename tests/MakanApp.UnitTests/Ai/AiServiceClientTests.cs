using System.Net;
using System.Text;
using MakanApp.Application.Ai;
using MakanApp.Infrastructure.Ai;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiServiceClientTests
{
    [Fact]
    public async Task ChatAsync_SendsCredentialCorrelationAndContract_AndParsesResponse()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler((request, _) =>
        {
            captured = request;
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidResponse()));
        });
        var client = CreateClient(handler);

        var result = await client.ChatAsync(Request(), CancellationToken.None);

        Assert.Equal("پاسخ", result.Answer.Text);
        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("service-token", captured.Headers.Authorization?.Parameter);
        Assert.Equal("corr-1", captured.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal("v1", captured.Headers.GetValues("X-AI-Contract-Version").Single());
        Assert.Equal(1, handler.CallCount);
    }

    public static TheoryData<HttpStatusCode, string> ErrorMappings => new()
    {
        { HttpStatusCode.BadRequest, AiServiceErrorCodes.InvalidRequest },
        { HttpStatusCode.Unauthorized, AiServiceErrorCodes.ServiceUnauthorized },
        { HttpStatusCode.Forbidden, AiServiceErrorCodes.ServiceForbidden },
        { HttpStatusCode.UnprocessableEntity, AiServiceErrorCodes.InvalidRequest },
        { HttpStatusCode.TooManyRequests, AiServiceErrorCodes.RateLimited },
        { HttpStatusCode.InternalServerError, AiServiceErrorCodes.ServiceUnavailable },
        { HttpStatusCode.ServiceUnavailable, AiServiceErrorCodes.ServiceUnavailable },
        { HttpStatusCode.GatewayTimeout, AiServiceErrorCodes.ServiceTimeout }
    };

    [Theory]
    [MemberData(nameof(ErrorMappings))]
    public async Task ChatAsync_MapsUpstreamStatusWithoutRetry(
        HttpStatusCode statusCode,
        string expectedCode)
    {
        var response = new HttpResponseMessage(statusCode);
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                TimeSpan.FromSeconds(7));
        }
        var handler = new StubHandler((_, _) => Task.FromResult(response));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(expectedCode, error.Code);
        Assert.Equal((int)statusCode, error.UpstreamStatusCode);
        Assert.Equal(1, handler.CallCount);
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            Assert.Equal(TimeSpan.FromSeconds(7), error.RetryAfter);
        }
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"requestId\":\"different\",\"answer\":{\"text\":\"ok\",\"language\":\"fa\"},\"insights\":[],\"risks\":[],\"recommendations\":[],\"sources\":[],\"metadata\":{\"modelAlias\":\"model\",\"processingTimeMs\":1}}")]
    [InlineData("{\"requestId\":\"req-1\",\"answer\":{\"text\":\"\",\"language\":\"fa\"},\"insights\":[],\"risks\":[],\"recommendations\":[],\"sources\":[],\"metadata\":{\"modelAlias\":\"model\",\"processingTimeMs\":1}}")]
    [InlineData("{\"requestId\":\"req-1\",\"answer\":{\"text\":\"ok\",\"language\":\"fa\"},\"insights\":[{\"type\":\"Learning\",\"severity\":\"Unknown\"}],\"risks\":[],\"recommendations\":[],\"sources\":[],\"metadata\":{\"modelAlias\":\"model\",\"processingTimeMs\":1}}")]
    [InlineData("{\"requestId\":\"req-1\",\"answer\":{\"text\":\"ok\",\"language\":\"fa\"},\"insights\":[],\"risks\":[],\"recommendations\":[],\"sources\":[],\"metadata\":{\"modelAlias\":\"model\",\"processingTimeMs\":1},\"unexpected\":true}")]
    public async Task ChatAsync_RejectsMalformedOrSchemaInvalidResponse(string body)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, body)));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ResponseInvalid, error.Code);
    }

    [Fact]
    public async Task ChatAsync_PreservesCallerCancellation()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(HttpStatusCode.OK, ValidResponse());
        });
        var client = CreateClient(handler);
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ChatAsync(Request(), source.Token));
    }

    [Fact]
    public async Task ChatAsync_MapsInternalTimeout()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(HttpStatusCode.OK, ValidResponse());
        });
        var client = CreateClient(handler, timeoutSeconds: 1);

        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ServiceTimeout, error.Code);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ChatAsync_OpensCircuitAfterConfiguredTransientFailures()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var client = CreateClient(handler, failureThreshold: 3);

        for (var index = 0; index < 3; index++)
        {
            await Assert.ThrowsAsync<AiServiceException>(() =>
                client.ChatAsync(Request(), CancellationToken.None));
        }
        var openError = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ServiceUnavailable, openError.Code);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task ChatAsync_Disabled_DoesNotResolveCredentialOrCallNetwork()
    {
        var handler = new StubHandler((_, _) => throw new InvalidOperationException());
        var credential = new StubCredentialProvider { ThrowIfCalled = true };
        var client = CreateClient(handler, enabled: false, credentialProvider: credential);

        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.FeatureDisabled, error.Code);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ChatAsync_CredentialFailure_IsSanitizedAndDoesNotCallNetwork()
    {
        var handler = new StubHandler((_, _) => throw new InvalidOperationException());
        var client = CreateClient(
            handler,
            credentialProvider: new StubCredentialProvider { ThrowIfCalled = true });

        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ServiceUnavailable, error.Code);
        Assert.DoesNotContain("Credential provider must not be called", error.Message);
        Assert.Equal(0, handler.CallCount);
    }

    private static AiServiceClient CreateClient(
        HttpMessageHandler handler,
        bool enabled = true,
        int timeoutSeconds = 5,
        int failureThreshold = 3,
        IAiServiceCredentialProvider? credentialProvider = null)
    {
        var options = new AiServiceOptions
        {
            Enabled = enabled,
            BaseUrl = "http://ai.test",
            TimeoutSeconds = timeoutSeconds,
            ContractVersion = "v1",
            CircuitBreakerFailureThreshold = failureThreshold,
            CircuitBreakerBreakSeconds = 30
        };
        return new AiServiceClient(
            new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan },
            credentialProvider ?? new StubCredentialProvider(),
            options,
            new AiServiceCircuitBreaker(options, TimeProvider.System));
    }

    private static AiChatRequest Request() => new(
        "req-1",
        "corr-1",
        null,
        new AiActor("user-1", "Student"),
        new AiScope("org-1", "school-1", null, "student-1"),
        new AiMessage("پرسش"),
        AiAuthorizedContext.Empty);

    private static string ValidResponse() =>
        "{\"requestId\":\"req-1\",\"correlationId\":\"corr-1\",\"conversationId\":null," +
        "\"answer\":{\"text\":\"پاسخ\",\"language\":\"fa\"},\"insights\":[],\"risks\":[]," +
        "\"recommendations\":[],\"sources\":[],\"metadata\":{\"modelAlias\":\"education-fast\",\"processingTimeMs\":3}}";

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) => new(statusCode)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubCredentialProvider : IAiServiceCredentialProvider
    {
        public bool ThrowIfCalled { get; init; }

        public ValueTask<AiServiceCredential> GetCredentialAsync(CancellationToken cancellationToken)
        {
            if (ThrowIfCalled)
            {
                throw new InvalidOperationException("Credential provider must not be called.");
            }
            return ValueTask.FromResult(new AiServiceCredential(
                "Bearer",
                "service-token",
                DateTime.UtcNow.AddMinutes(2)));
        }
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return send(request, cancellationToken);
        }
    }
}
