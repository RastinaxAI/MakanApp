using System.Net;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Ai;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed partial class AiServiceClientTests
{
    private const string SseStart = "event: start\ndata: {\"requestId\":\"req-1\",\"correlationId\":\"corr-1\",\"conversationId\":null,\"modelAlias\":\"education-fast\"}\n\n";
    private const string SseDelta = "event: delta\ndata: {\"sequence\":1,\"text\":\"پاسخ 😀\"}\n\n";
    private const string SseDone = "event: done\ndata: {\"requestId\":\"req-1\",\"correlationId\":\"corr-1\",\"conversationId\":null,\"metadata\":{\"modelAlias\":\"education-fast\",\"processingTimeMs\":1}}\n\n";
    private const string SseError = "event: error\ndata: {\"error\":{\"code\":\"AI_PROVIDER_TIMEOUT\",\"message\":\"private-upstream-secret\",\"requestId\":\"req-1\"}}\n\n";

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(7)] [InlineData(2048)]
    public async Task StreamParsesFragmentedUtf8AndCrLfWithoutCredentialOrBuffering(int chunk)
    {
        var stream = new FragmentedSseStream(Encoding.UTF8.GetBytes((": heartbeat\n\n" + SseStart + SseDelta + SseDone).Replace("\n", "\r\n")), chunk);
        var handler = new StubHandler(async (request, ct) =>
        {
            Assert.Equal("/api/v1/ai/chat/stream", request.RequestUri!.AbsolutePath);
            Assert.Equal("text/event-stream", Assert.Single(request.Headers.Accept).MediaType);
            Assert.Equal("v1", request.Headers.GetValues("X-AI-Contract-Version").Single()); Assert.Null(request.Headers.Authorization);
            var wire = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal(12, wire.RootElement.GetProperty("context").GetProperty("assistantProfile").GetProperty("gradeLevel").GetInt32());
            Assert.Equal("User", wire.RootElement.GetProperty("history")[0].GetProperty("role").GetString());
            return SseResponse(stream);
        });
        var client = CreateClient(handler, useServiceAuthentication: false, credentialProvider: new StubCredentialProvider { ThrowIfCalled = true });
        var request = Request() with { Context = Request().Context with { AssistantProfile = new(12, AiAssistantProficiency.Beginner) }, History = [new("User", "old q"), new("Assistant", "old a")] };
        await using var iterator = client.StreamChatAsync(request, default).GetAsyncEnumerator();
        Assert.True(await iterator.MoveNextAsync()); Assert.IsType<AiChatStreamStart>(iterator.Current);
        Assert.False(stream.ReachedEnd);
        Assert.True(await iterator.MoveNextAsync()); Assert.Equal("پاسخ 😀", Assert.IsType<AiChatStreamDelta>(iterator.Current).Text);
        Assert.True(await iterator.MoveNextAsync()); Assert.IsType<AiChatStreamDone>(iterator.Current); Assert.True(stream.ReachedEnd);
        Assert.False(await iterator.MoveNextAsync()); Assert.Equal(1, handler.CallCount);
    }

    public static IEnumerable<object[]> InvalidSseFrames()
    {
        yield return [SseDelta + SseDone];
        yield return [SseStart + SseStart + SseDelta + SseDone];
        yield return [SseStart + SseDelta + SseDelta + SseDone];
        yield return [SseStart + SseDelta.Replace("\"sequence\":1", "\"sequence\":2") + SseDone];
        yield return [SseStart + SseDelta.Replace("\"sequence\":1", "\"sequence\":0") + SseDone];
        yield return [SseStart.Replace("req-1", "foreign") + SseDelta + SseDone];
        yield return [SseStart.Replace("corr-1", "foreign") + SseDelta + SseDone];
        yield return [SseStart.Replace("\"conversationId\":null", "\"conversationId\":\"foreign\"") + SseDelta + SseDone];
        yield return [SseStart.Replace("education-fast", "forged") + SseDelta + SseDone];
        yield return [SseStart.Replace("\"modelAlias\":", "\"provenance\":{},\"modelAlias\":") + SseDelta + SseDone];
        yield return [SseStart + SseDone];
        yield return [SseStart + SseDelta];
        yield return [SseStart + SseDelta.Replace("پاسخ 😀", "  ") + SseDone];
        yield return [SseStart + SseDelta + SseDone + SseDelta];
        yield return [SseStart + SseDelta + SseDone + SseDone];
        yield return [SseStart + SseDelta + SseDone.Replace("req-1", "foreign")];
        yield return [SseStart + SseDelta + SseDone.Replace("\"processingTimeMs\":1", "\"processingTimeMs\":-1")];
        yield return [SseStart + SseDelta.Replace("\"sequence\":1", "\"sequence\":\"1\"") + SseDone];
        yield return [SseStart + "event: delta\ndata: {}\n\n" + SseDone];
        yield return [SseStart + "event: delta\ndata: {broken}\n\n" + SseDone];
        yield return [SseStart + SseDelta.Replace("\"sequence\":1", "\"sequence\":1,\"sequence\":1") + SseDone];
        yield return [SseStart + SseDelta.Replace("\"sequence\":1", "\"sequence\":1,\"private\":\"secret\"") + SseDone];
        yield return [SseStart + SseDelta.Replace("event: delta", "event: unknown") + SseDone];
        yield return [SseStart + SseDelta + SseDone.TrimEnd()];
        yield return [SseStart + SseDelta.Replace("پاسخ 😀", "") + SseDone];
        yield return [SseStart + SseDelta.Replace("پاسخ 😀", new string('x', 32769)) + SseDone];
        yield return [SseStart + SseError.Replace("AI_PROVIDER_TIMEOUT", "UNTRUSTED_SECRET_CODE") + SseDone];
    }

    [Theory]
    [MemberData(nameof(InvalidSseFrames))]
    public async Task MalformedStreamNeverYieldsSuccessOrRetries(string wire)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(SseResponse(new FragmentedSseStream(Encoding.UTF8.GetBytes(wire), 17))));
        var completed = false;
        var error = await Assert.ThrowsAsync<AiServiceException>(async () =>
        {
            await foreach (var value in CreateClient(handler, useServiceAuthentication: false).StreamChatAsync(Request(), default))
                completed |= value is AiChatStreamDone;
        });
        Assert.False(completed); Assert.Equal(AiServiceErrorCodes.ResponseInvalid, error.Code); Assert.Equal(1, handler.CallCount);
        Assert.Null(error.InnerException); Assert.DoesNotContain("private", error.ToString());
    }

    [Fact]
    public async Task PartialProviderErrorIsTerminalFailureAndSanitized()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(SseResponse(new MemoryStream(Encoding.UTF8.GetBytes(SseStart + SseDelta + SseError + SseDone)))));
        var types = new List<string>();
        var error = await Assert.ThrowsAsync<AiServiceException>(async () =>
        {
            await foreach (var value in CreateClient(handler, useServiceAuthentication: false).StreamChatAsync(Request(), default)) types.Add(value.EventName);
        });
        Assert.Equal(new[] { "start", "delta" }, types); Assert.Equal(AiServiceErrorCodes.ServiceTimeout, error.Code);
        Assert.Equal("AI_PROVIDER_TIMEOUT", error.UpstreamErrorCode); Assert.DoesNotContain("private-upstream-secret", error.ToString());
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task StreamCancellationReachesUpstreamAndDoesNotRetry()
    {
        var stream = new BlockingSseStream(Encoding.UTF8.GetBytes(SseStart + SseDelta));
        var handler = new StubHandler((_, _) => Task.FromResult(SseResponse(stream)));
        using var ct = new CancellationTokenSource();
        await using var iterator = CreateClient(handler, useServiceAuthentication: false).StreamChatAsync(Request(), ct.Token).GetAsyncEnumerator(ct.Token);
        Assert.True(await iterator.MoveNextAsync()); Assert.True(await iterator.MoveNextAsync());
        var pending = iterator.MoveNextAsync().AsTask(); await stream.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(5)); ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(stream.Canceled); Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task InvalidUtf8AndOversizedFrameFailClosed()
    {
        foreach (var bytes in new[] { new byte[] { 0xff, 0xfe }, Encoding.UTF8.GetBytes("event: delta\ndata: " + new string('x', 400000)) })
        {
            var handler = new StubHandler((_, _) => Task.FromResult(SseResponse(new MemoryStream(bytes))));
            var error = await Assert.ThrowsAsync<AiServiceException>(async () => { await foreach (var _ in CreateClient(handler).StreamChatAsync(Request(), default)) { } });
            Assert.Equal(AiServiceErrorCodes.ResponseInvalid, error.Code);
        }
    }

    [Theory]
    [InlineData("organizationId")] [InlineData("schoolId")] [InlineData("classId")] [InlineData("studentId")]
    public async Task PersonalScopeCannotCarryAnyOrganizationSelectorBeforeProvider(string field)
    {
        var scope = field switch { "organizationId" => new AiScope("forged", null, null, null), "schoolId" => new(null, "forged", null, null),
            "classId" => new(null, null, "forged", null), _ => new AiScope(null, null, null, "forged") };
        var handler = new StubHandler((_, _) => throw new InvalidOperationException());
        var error = await Assert.ThrowsAsync<AiServiceException>(() => CreateClient(handler).ChatAsync(Request() with { Actor = new("user", "Personal"), Scope = scope }, default));
        Assert.Equal(AiServiceErrorCodes.InvalidRequest, error.Code); Assert.Equal(0, handler.CallCount);
    }

    private static HttpResponseMessage SseResponse(Stream stream)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
        response.Content.Headers.ContentType = new("text/event-stream"); return response;
    }
    private sealed class FragmentedSseStream(byte[] bytes, int chunk) : MemoryStream(bytes)
    {
        public bool ReachedEnd { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await base.ReadAsync(buffer[..Math.Min(buffer.Length, chunk)], cancellationToken);
            ReachedEnd |= read == 0; return read;
        }
    }
    private sealed class BlockingSseStream(byte[] bytes) : MemoryStream(bytes)
    {
        public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Canceled { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position < Length) return await base.ReadAsync(buffer, cancellationToken);
            Waiting.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
            catch (OperationCanceledException) { Canceled = true; throw; }
        }
    }
}
