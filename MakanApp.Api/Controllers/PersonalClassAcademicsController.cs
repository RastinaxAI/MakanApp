using MakanApp.Api.Authentication;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/academic/personal-classes/{classId:guid}")]
public sealed class PersonalClassAcademicsController(IAssignmentService assignments, IExamService exams, PersonalAcademicReadService reads) : ControllerBase
{
    [HttpPost("assignments")]
    [ProducesResponseType<AssignmentResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AssignmentResult>> CreateAssignment(Guid classId, CreateAssignmentDraftCommand command, CancellationToken ct) =>
        StatusCode(201, await assignments.CreateDraftAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            classId, command, ct, AcademicClassKind.PersonalClass));

    [HttpGet("assignments")]
    public async Task<ActionResult<AcademicPage<AssignmentResult>>> Assignments(Guid classId, [FromQuery] AcademicListQuery query, CancellationToken ct) =>
        Ok(await reads.AssignmentsAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), classId, query, ct));

    [HttpPost("exams")]
    [ProducesResponseType<ExamEditorDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ExamEditorDto>> CreateExam(Guid classId, CreateExamDraftCommand command, CancellationToken ct) =>
        StatusCode(201, await exams.CreateDraftAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            classId, command, ct, AcademicClassKind.PersonalClass));

    [HttpGet("exams")]
    public async Task<ActionResult<AcademicPage<StudentExamSummary>>> Exams(Guid classId, [FromQuery] AcademicListQuery query, CancellationToken ct) =>
        Ok(await reads.ExamsAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), classId, query, ct));
}
