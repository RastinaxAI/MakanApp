using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[RequestSizeLimit(64 * 1024)]
[EnableRateLimiting("messaging-send")]
public sealed class ConversationChecklistsController(ConversationChecklistService service) : ControllerBase
{
    private Guid UserId => AuthenticatedSession.GetUserId(User);
    private Guid SessionId => AuthenticatedSession.GetSessionId(User);

    [HttpPost("api/v1/conversations/{conversationId:guid}/checklists")]
    [ProducesResponseType<MessageReceiptResult>(StatusCodes.Status200OK)]
    public Task<MessageReceiptResult> Create(Guid conversationId, CreateConversationChecklistCommand command, CancellationToken ct) =>
        service.CreateAsync(UserId, SessionId, conversationId, command, ct);

    [HttpGet("api/v1/conversations/{conversationId:guid}/messages/{messageId:guid}/checklist")]
    [ProducesResponseType<ConversationChecklistResult>(StatusCodes.Status200OK)]
    public Task<ConversationChecklistResult> Get(Guid conversationId, Guid messageId, CancellationToken ct) =>
        service.GetAsync(UserId, SessionId, conversationId, messageId, ct);

    [HttpPut("api/v1/conversations/{conversationId:guid}/messages/{messageId:guid}/checklist/items/{itemId:guid}")]
    [ProducesResponseType<ConversationChecklistResult>(StatusCodes.Status200OK)]
    public Task<ConversationChecklistResult> SetItem(Guid conversationId, Guid messageId, Guid itemId, SetChecklistItemCommand command, CancellationToken ct) =>
        service.SetItemAsync(UserId, SessionId, conversationId, messageId, itemId, command, ct);

    [HttpPut("api/v1/conversations/{conversationId:guid}/messages/{messageId:guid}/checklist")]
    [ProducesResponseType<ConversationChecklistResult>(StatusCodes.Status200OK)]
    public Task<ConversationChecklistResult> Update(Guid conversationId, Guid messageId, UpdateConversationChecklistCommand command, CancellationToken ct) =>
        service.UpdateAsync(UserId, SessionId, conversationId, messageId, command, HttpContext.TraceIdentifier, ct);
}
