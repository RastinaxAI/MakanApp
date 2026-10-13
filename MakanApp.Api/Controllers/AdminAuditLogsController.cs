using MakanApp.Application.Platform;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/v1/audit-logs")]
public sealed class AdminAuditLogsController(IPlatformIamService platformIamService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PlatformPermissionKeys.AuditRead)]
    [ProducesResponseType<AdminAuditLogPage>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminAuditLogPage>> GetAuditLogs(
        [FromQuery] Guid? actorUserId,
        [FromQuery] string? action,
        [FromQuery] string? targetType,
        [FromQuery] string? targetId,
        [FromQuery] int? result,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string sortBy = "occurredAtUtc",
        [FromQuery] string sortDirection = "desc",
        CancellationToken cancellationToken = default) =>
        Ok(await platformIamService.GetAuditLogsAsync(
            new AdminAuditLogQuery(
                actorUserId,
                action,
                targetType,
                targetId,
                result,
                fromUtc,
                toUtc,
                page,
                pageSize,
                sortBy,
                sortDirection),
            cancellationToken));
}
