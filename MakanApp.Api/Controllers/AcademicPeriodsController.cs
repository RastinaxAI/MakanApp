using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/academic/periods")]
public sealed class AcademicPeriodsController(IAcademicService academicService, SchoolReadService schoolReads) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SchoolPage<AcademicPeriodResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolPage<AcademicPeriodResult>>> Get([FromQuery] SchoolPageQuery query,
        [FromQuery] MakanApp.Domain.Academic.AcademicPeriodStatus? status = null, CancellationToken ct = default) =>
        Ok(await schoolReads.PeriodsAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), query, Request.Query.ContainsKey(nameof(status)) ? status : MakanApp.Domain.Academic.AcademicPeriodStatus.Active, ct));

    [HttpPost]
    [ProducesResponseType<AcademicPeriodResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AcademicPeriodResult>> Create(
        CreateAcademicPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var result = await academicService.CreateAcademicPeriodAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
