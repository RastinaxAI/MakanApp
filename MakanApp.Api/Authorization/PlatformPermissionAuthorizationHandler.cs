using System.Security.Claims;
using MakanApp.Application.Platform;
using Microsoft.AspNetCore.Authorization;

namespace MakanApp.Api.Authorization;

public sealed class PlatformPermissionAuthorizationHandler(
    IPlatformPermissionResolver permissionResolver)
    : AuthorizationHandler<PlatformPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformPermissionRequirement requirement)
    {
        var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;
        if (Guid.TryParse(value, out var userId) &&
            await permissionResolver.HasPermissionAsync(
                userId,
                requirement.Permission,
                cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
