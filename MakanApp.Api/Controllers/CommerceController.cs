using MakanApp.Api.Authentication;
using MakanApp.Application.Commerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/commerce")]
public sealed class CommerceController(ICommerceService commerceService) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<ManagerCommerceSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagerCommerceSummaryDto>> GetSummary(
        CancellationToken cancellationToken) =>
        Ok(await commerceService.GetManagerSummaryAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken));
}
