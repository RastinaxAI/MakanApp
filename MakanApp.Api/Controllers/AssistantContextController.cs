using MakanApp.Api.Authentication;
using MakanApp.Application.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/v1/ai/assistant-context")]
public sealed class AssistantContextController(AssistantContextResolver contexts) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AssistantContextResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<AssistantContextResult?>> Read(CancellationToken ct) => Ok(await contexts.ReadAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), ct));
    [HttpPut]
    public async Task<ActionResult<AssistantContextResult>> Select(SelectAssistantContextCommand command, CancellationToken ct) =>
        Ok(await contexts.SelectAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), command, ct));
}
