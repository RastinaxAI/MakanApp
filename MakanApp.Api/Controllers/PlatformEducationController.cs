using MakanApp.Application.Platform;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Authorize(Policy = PlatformPermissionKeys.EducationRead)]
[Route("api/admin/v1/education")]
public sealed class PlatformEducationController(
    IPlatformEducationSafetyService service) : ControllerBase
{
    [HttpGet("classes")]
    public async Task<ActionResult<PlatformClassPage>> GetClasses(
        [FromQuery] Guid? organizationId,
        [FromQuery] string? search,
        [FromQuery] ClassStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetClassesAsync(
            new PlatformClassQuery(organizationId, search, status, page, pageSize),
            cancellationToken));

    [HttpGet("classes/{classId:guid}")]
    public async Task<ActionResult<PlatformClassDetail>> GetClass(
        Guid classId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetClassAsync(classId, cancellationToken));

    [HttpGet("teachers")]
    public async Task<ActionResult<PlatformTeacherAssignmentPage>> GetTeachers(
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? classId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetTeachersAsync(
            new PlatformEducationQuery(organizationId, classId, search, page, pageSize),
            cancellationToken));

    [HttpGet("enrollments")]
    public async Task<ActionResult<PlatformEnrollmentPage>> GetEnrollments(
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? classId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetEnrollmentsAsync(
            new PlatformEducationQuery(organizationId, classId, search, page, pageSize),
            cancellationToken));

    [HttpGet("assignments")]
    public async Task<ActionResult<PlatformAssignmentPage>> GetAssignments(
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? classId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetAssignmentsAsync(
            new PlatformEducationQuery(organizationId, classId, search, page, pageSize),
            cancellationToken));

    [HttpGet("exams")]
    public async Task<ActionResult<PlatformExamPage>> GetExams(
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? classId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetExamsAsync(
            new PlatformEducationQuery(organizationId, classId, search, page, pageSize),
            cancellationToken));
}
