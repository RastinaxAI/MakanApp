using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[RequestSizeLimit(64 * 1024)]
[Route("api/v1/conversations/{conversationId:guid}/topics")]
[EnableRateLimiting("messaging-send")]
public sealed class ConversationTopicsController(ConversationTopicService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ConversationTopicPageResult>(StatusCodes.Status200OK)]
    public Task<ConversationTopicPageResult> Get(Guid conversationId, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 30) =>
        service.GetAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), conversationId, page, pageSize, ct);
    [HttpPost]
    [ProducesResponseType<ConversationTopicResult>(StatusCodes.Status200OK)]
    public Task<ConversationTopicResult> Create(Guid conversationId, CreateConversationTopicCommand command, CancellationToken ct) =>
        service.CreateAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), conversationId, command, HttpContext.TraceIdentifier, ct);
    [HttpPut("{topicId:guid}")]
    [ProducesResponseType<ConversationTopicResult>(StatusCodes.Status200OK)]
    public Task<ConversationTopicResult> Update(Guid conversationId, Guid topicId, UpdateConversationTopicCommand command, CancellationToken ct) =>
        service.UpdateAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), conversationId, topicId, command, HttpContext.TraceIdentifier, ct);
}
