using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SchoolAcademicDiscoveryController(SchoolAcademicDiscoveryService service) : ControllerBase
{
    [HttpGet("api/v1/organization/teacher-content-activities")]
    [ProducesResponseType<SchoolPage<TeacherContentActivityDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolPage<TeacherContentActivityDto>>> Activities([FromQuery] TeacherContentActivityQuery query, CancellationToken ct) =>
        Ok(await service.ActivitiesAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), query, ct));
    [HttpGet("api/v1/academic/exams/discovery")]
    [ProducesResponseType<AcademicPage<StudentExamSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AcademicPage<StudentExamSummary>>> Exams([FromQuery] OrganizationExamQuery query, CancellationToken ct) =>
        Ok(await service.ExamsAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), query, ct));
}
