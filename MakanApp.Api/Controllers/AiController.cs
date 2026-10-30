using MakanApp.Api.Authentication;
using MakanApp.Application.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("ai-chat")]
[Route("api/v1/ai")]
public sealed class AiController(
    IAiChatService chatService,
    IAiRoleContextService roleContextService,
    IAiSourceSelectionService sourceSelection) : ControllerBase
{
    [HttpPost("sources/preflight")]
    [ProducesResponseType<AiSourcePreflightResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiSourcePreflightResult>> SourcePreflight(
        [FromBody] AiSourcePreflightCommand command, CancellationToken cancellationToken) =>
        Ok(await sourceSelection.PreflightAsync(AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User), command, cancellationToken));

    [HttpPost("chat")]
    [ProducesResponseType<AiChatResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiChatResult>> Chat(
        [FromBody] SendAiChatCommand command,
        CancellationToken cancellationToken) =>
        Ok(await chatService.ChatAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            command,
            cancellationToken,
            Invocation(command.OperationId)));

    [HttpPost("students/analyze")]
    [ProducesResponseType<AiStudentAnalysisResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiStudentAnalysisResponse>> AnalyzeStudent(
        CancellationToken cancellationToken) =>
        Ok(await roleContextService.AnalyzeStudentAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            cancellationToken,
            Invocation()));

    [HttpPost("study-plan")]
    [ProducesResponseType<AiStudyPlanResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiStudyPlanResponse>> CreateStudyPlan(
        [FromBody] CreateStudyPlanCommand command,
        CancellationToken cancellationToken) =>
        Ok(await roleContextService.CreateStudyPlanAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            command,
            cancellationToken,
            Invocation(command.OperationId)));

    [HttpPost("homeworks/{assignmentId:guid}/explain")]
    [ProducesResponseType<AiHomeworkExplanationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiHomeworkExplanationResponse>> ExplainHomework(
        Guid assignmentId,
        [FromBody] ExplainHomeworkCommand command,
        CancellationToken cancellationToken) =>
        Ok(await roleContextService.ExplainHomeworkAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            assignmentId,
            command,
            cancellationToken,
            Invocation(command.OperationId)));

    [HttpPost("exams/{examId:guid}/prepare")]
    [ProducesResponseType<AiExamPreparationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiExamPreparationResponse>> PrepareExam(
        Guid examId,
        CancellationToken cancellationToken) =>
        Ok(await roleContextService.PrepareExamAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            examId,
            cancellationToken,
            Invocation()));

    [HttpPost("parents/child-summary")]
    [ProducesResponseType<AiParentChildSummaryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiParentChildSummaryResponse>> SummarizeChild(
        CancellationToken cancellationToken) =>
        Ok(await roleContextService.SummarizeChildAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            cancellationToken,
            Invocation()));

    [HttpPost("teachers/classes/{classId:guid}/summary")]
    [ProducesResponseType<AiRoleInsightResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiRoleInsightResponse>> SummarizeTeacherClass(
        Guid classId,
        CancellationToken cancellationToken) =>
        Ok(await roleContextService.SummarizeTeacherClassAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            classId,
            cancellationToken,
            Invocation()));

    [HttpPost("teachers/classes/{classId:guid}/students/{studentId:guid}/insight")]
    [ProducesResponseType<AiRoleInsightResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiRoleInsightResponse>> GetTeacherStudentInsight(
        Guid classId,
        Guid studentId,
        CancellationToken cancellationToken) =>
        Ok(await roleContextService.GetTeacherStudentInsightAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            classId,
            studentId,
            cancellationToken,
            Invocation()));

    [HttpPost("teachers/classes/{classId:guid}/activity-suggestions")]
    [ProducesResponseType<AiActivitySuggestionsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiActivitySuggestionsResponse>> SuggestTeacherActivities(
        Guid classId,
        [FromBody] SuggestTeacherActivitiesCommand command,
        CancellationToken cancellationToken) =>
        Ok(await roleContextService.SuggestTeacherActivitiesAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            classId,
            command,
            cancellationToken,
            Invocation(command.OperationId)));

    [HttpPost("managers/school-summary")]
    [ProducesResponseType<AiRoleInsightResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiRoleInsightResponse>> SummarizeManagerSchool(
        CancellationToken cancellationToken) =>
        Ok(await roleContextService.SummarizeManagerSchoolAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            cancellationToken,
            Invocation()));

    [HttpPost("managers/classes/{classId:guid}/insight")]
    [ProducesResponseType<AiRoleInsightResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiRoleInsightResponse>> GetManagerClassInsight(
        Guid classId,
        CancellationToken cancellationToken) =>
        Ok(await roleContextService.GetManagerClassInsightAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            classId,
            cancellationToken,
            Invocation()));

    [HttpPost("managers/classes/{classId:guid}/students/{studentId:guid}/insight")]
    [ProducesResponseType<AiRoleInsightResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiRoleInsightResponse>> GetManagerStudentInsight(
        Guid classId,
        Guid studentId,
        CancellationToken cancellationToken) =>
        Ok(await roleContextService.GetManagerStudentInsightAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            classId,
            studentId,
            cancellationToken,
            Invocation()));
    private AiInvocationOptions Invocation(Guid? bodyId = null)
    {
        var ids = Request.Headers["X-AI-Operation-Id"];
        var payers = Request.Headers["X-AI-Payer"];
        Guid? operationId = null;
        MakanApp.Domain.Commerce.AiPayerSelection? payer = null;
        if (ids.Count > 1 || payers.Count > 1 || ids.Count == 1 && !Guid.TryParse(ids[0], out _)) throw InvalidHeaders();
        if (ids.Count == 1) operationId = Guid.Parse(ids[0]!);
        if (payers.Count == 1)
        {
            if (!Enum.TryParse<MakanApp.Domain.Commerce.AiPayerSelection>(payers[0], out var parsed) || !Enum.IsDefined(parsed)) throw InvalidHeaders();
            payer = parsed;
        }
        var options = operationId.HasValue || bodyId.HasValue
            ? AiInvocationOptions.Resolve(new(operationId, payer), bodyId) : new AiInvocationOptions(null, payer);
        if (options.OperationId.HasValue) Response.Headers["X-AI-Operation-Id"] = options.OperationId.Value.ToString("D");
        return options;
    }
    private static AiServiceException InvalidHeaders() => new(AiServiceErrorCodes.InvalidRequest, "Header عملیات AI معتبر نیست.");

}
