using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[Route("api/v1/academic")]
public sealed class ClassLessonsController(ClassLessonService service) : ControllerBase
{
    [HttpGet("classes/{classId:guid}/lessons")]
    [ProducesResponseType<SchoolPage<ClassLessonResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolPage<ClassLessonResult>>> List(Guid classId, [FromQuery] SchoolPageQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(UserId(), SessionId(), classId, query, ct));
    [HttpPost("classes/{classId:guid}/lessons")]
    [ProducesResponseType<ClassLessonResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ClassLessonResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassLessonResult>> Create(Guid classId, CreateClassLessonCommand command, CancellationToken ct)
    {
        var result = await service.CreateAsync(UserId(), SessionId(), classId, command, ct);
        return StatusCode(result.Replayed ? 200 : 201, result);
    }
    [HttpGet("lessons/{lessonId:guid}")]
    [ProducesResponseType<ClassLessonResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassLessonResult>> Get(Guid lessonId, CancellationToken ct) => Ok(await service.GetAsync(UserId(), SessionId(), lessonId, ct));
    [HttpPatch("lessons/{lessonId:guid}")]
    [ProducesResponseType<ClassLessonResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassLessonResult>> Update(Guid lessonId, UpdateClassLessonCommand command, CancellationToken ct) => Ok(await service.UpdateAsync(UserId(), SessionId(), lessonId, command, ct));
    [HttpPost("lessons/{lessonId:guid}/publish")]
    [ProducesResponseType<ClassLessonResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassLessonResult>> Publish(Guid lessonId, PublishClassLessonCommand command, CancellationToken ct) => Ok(await service.PublishAsync(UserId(), SessionId(), lessonId, command, ct));
    private Guid UserId() => AuthenticatedSession.GetUserId(User);
    private Guid SessionId() => AuthenticatedSession.GetSessionId(User);
}
