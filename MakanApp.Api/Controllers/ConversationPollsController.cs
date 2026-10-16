using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[RequestSizeLimit(64 * 1024)]
[EnableRateLimiting("messaging-send")]
public sealed class ConversationPollsController(ConversationPollService service) : ControllerBase
{
    private Guid UserId => AuthenticatedSession.GetUserId(User);
    private Guid SessionId => AuthenticatedSession.GetSessionId(User);
    [HttpPost("api/v1/conversations/{conversationId:guid}/polls")]
    [ProducesResponseType<MessageReceiptResult>(StatusCodes.Status200OK)]
    public Task<MessageReceiptResult> Create(Guid conversationId, CreateConversationPollCommand command, CancellationToken ct) => service.CreateAsync(UserId, SessionId, conversationId, command, ct);
    [HttpGet("api/v1/conversations/{conversationId:guid}/messages/{messageId:guid}/poll")]
    [ProducesResponseType<ConversationPollResult>(StatusCodes.Status200OK)]
    public Task<ConversationPollResult> Get(Guid conversationId, Guid messageId, CancellationToken ct) => service.GetAsync(UserId, SessionId, conversationId, messageId, ct);
    [HttpPost("api/v1/conversations/{conversationId:guid}/messages/{messageId:guid}/poll/vote")]
    [ProducesResponseType<ConversationPollResult>(StatusCodes.Status200OK)]
    public Task<ConversationPollResult> Vote(Guid conversationId, Guid messageId, VoteConversationPollCommand command, CancellationToken ct) => service.VoteAsync(UserId, SessionId, conversationId, messageId, command, ct);
    [HttpPost("api/v1/conversations/{conversationId:guid}/messages/{messageId:guid}/poll/close")]
    [ProducesResponseType<ConversationPollResult>(StatusCodes.Status200OK)]
    public Task<ConversationPollResult> Close(Guid conversationId, Guid messageId, CloseConversationPollCommand command, CancellationToken ct) => service.CloseAsync(UserId, SessionId, conversationId, messageId, command, HttpContext.TraceIdentifier, ct);
    [HttpGet("api/v1/conversations/{conversationId:guid}/messages/{messageId:guid}/poll/voters")]
    [ProducesResponseType<ConversationPollVoterPageResult>(StatusCodes.Status200OK)]
    public Task<ConversationPollVoterPageResult> Voters(Guid conversationId, Guid messageId, CancellationToken ct, [FromQuery] int? optionNumber = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 30) => service.GetVotersAsync(UserId, SessionId, conversationId, messageId, optionNumber, page, pageSize, ct);
}
