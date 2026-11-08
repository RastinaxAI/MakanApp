using System.Net;
using System.Text.Json;
using MakanApp.Application.Ai;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed partial class AiServiceClientTests
{
    [Theory]
    [InlineData(422, "AI_INVALID_REQUEST", AiServiceErrorCodes.InvalidRequest)]
    [InlineData(422, "AI_INVALID_CONTEXT", AiServiceErrorCodes.InvalidRequest)]
    [InlineData(503, "AI_MODEL_UNAVAILABLE", AiServiceErrorCodes.ServiceUnavailable)]
    [InlineData(504, "AI_PROVIDER_TIMEOUT", AiServiceErrorCodes.ServiceTimeout)]
    [InlineData(429, "AI_RATE_LIMITED", AiServiceErrorCodes.RateLimited)]
    [InlineData(502, "AI_RESPONSE_INVALID", AiServiceErrorCodes.ResponseInvalid)]
    [InlineData(502, "AI_INTERNAL_ERROR", AiServiceErrorCodes.ResponseInvalid)]
    public async Task FrozenBusinessErrorEnvelopePreservesSafeUpstreamCodeWithoutRetry(int status, string upstream, string mapped)
    {
        var body = JsonSerializer.Serialize(new { error = new { code = upstream, message = "private context must never be logged" } });
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse((HttpStatusCode)status, body)));
        var error = await Assert.ThrowsAsync<AiServiceException>(() => CreateClient(handler).ChatAsync(Request(), default));
        Assert.Equal(mapped, error.Code); Assert.Equal(upstream, error.UpstreamErrorCode);
        Assert.Equal(status, error.UpstreamStatusCode); Assert.Equal(1, handler.CallCount);
        Assert.DoesNotContain("private context", error.Message);
    }

    [Fact]
    public async Task UnexpectedErrorCodeOrStatusPairCannotInjectPublicCode()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.UnprocessableEntity,
            "{\"error\":{\"code\":\"AI_INTERNAL_ERROR\",\"message\":\"private\"}}")));
        var error = await Assert.ThrowsAsync<AiServiceException>(() => CreateClient(handler).ChatAsync(Request(), default));
        Assert.Equal(AiServiceErrorCodes.InvalidRequest, error.Code); Assert.Null(error.UpstreamErrorCode);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"error\":[]}")]
    public async Task NonObjectErrorEnvelopePreservesStatusMappingWithoutRetry(string body)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.BadGateway, body)));
        var error = await Assert.ThrowsAsync<AiServiceException>(() => CreateClient(handler).ChatAsync(Request(), default));
        Assert.Equal(AiServiceErrorCodes.ResponseInvalid, error.Code);
        Assert.Null(error.UpstreamErrorCode);
        Assert.Equal(502, error.UpstreamStatusCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData(" corr ")]
    [InlineData("corr\r\ninjected")]
    public async Task CorrelationIsRejectedRatherThanSilentlyTrimmedOrInsertedIntoHeader(string correlation)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidResponse())));
        var error = await Assert.ThrowsAsync<AiServiceException>(() => CreateClient(handler).ChatAsync(Request() with { CorrelationId = correlation }, default));
        Assert.Equal(AiServiceErrorCodes.InvalidRequest, error.Code); Assert.Equal(0, handler.CallCount);
    }
}
