using System.Diagnostics;
using System.Text.Json;
using MakanApp.Api.Authentication;
using MakanApp.Application.Ai;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MakanApp.Api.Controllers;

public sealed partial class AiController
{
    [HttpGet("conversations/{conversationId:guid}/history")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None, VaryByHeader = "Authorization")]
    [ProducesResponseType<AiConversationHistoryResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiConversationHistoryResult>> History(Guid conversationId, CancellationToken cancellationToken) =>
        Ok(await chatService.HistoryAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), conversationId, cancellationToken));

    [HttpPost("chat/stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task StreamChat([FromBody] SendAiChatCommand command,
        [FromServices] IOptions<JsonOptions> json, [FromServices] ILogger<AiController> logger, CancellationToken cancellationToken)
    {
        await using var events = chatService.StreamChatAsync(AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User), HttpContext.TraceIdentifier, command, cancellationToken,
            Invocation(command.OperationId)).GetAsyncEnumerator(cancellationToken);
        // admission و خطای پیش از start هنوز ProblemDetails عادی برمی‌گردانند.
        if (!await events.MoveNextAsync()) throw new AiServiceException(AiServiceErrorCodes.ResponseInvalid, "جریان AI شروع نشد.");
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Accel-Buffering"] = "no";
        string? requestId = null;
        try
        {
            do
            {
                if (events.Current is AiChatStreamStart start) requestId = start.RequestId;
                await Write(events.Current);
            } while (await events.MoveNextAsync());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("AI stream canceled. ErrorCode={ErrorCode} CorrelationId={CorrelationId} TraceId={TraceId}",
                "AI_STREAM_CANCELED", HttpContext.TraceIdentifier, Activity.Current?.TraceId.ToString());
            throw;
        }
        catch (Exception error)
        {
            // متن Exception و بدنهٔ خصوصی هرگز وارد SSE یا Log نمی‌شوند.
            var code = error is AiServiceException ai && ai.Code is AiServiceErrorCodes.ResponseInvalid or
                AiServiceErrorCodes.ServiceTimeout or AiServiceErrorCodes.ServiceUnavailable or AiServiceErrorCodes.RateLimited or
                AiServiceErrorCodes.ContextNotAllowed ? ai.Code : "AI_STREAM_FAILED";
            logger.LogError("AI stream failed. ErrorCode={ErrorCode} CorrelationId={CorrelationId} TraceId={TraceId}",
                code, HttpContext.TraceIdentifier, Activity.Current?.TraceId.ToString());
            if (!Response.HasStarted) throw;
            await Write(new AiChatStreamError(new(code, "جریان پاسخ کامل نشد؛ این پاسخ موفق ثبت نشده است.", requestId)));
        }

        async Task Write(AiChatStreamEvent item)
        {
            var data = JsonSerializer.Serialize(item, item.GetType(), json.Value.JsonSerializerOptions);
            await Response.WriteAsync($"event: {item.EventName}\ndata: {data}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
