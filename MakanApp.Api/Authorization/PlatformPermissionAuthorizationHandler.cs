using System.Security.Claims;
using MakanApp.Api.Authentication;
using MakanApp.Application.Platform;
using Microsoft.AspNetCore.Authorization;

namespace MakanApp.Api.Authorization;

public sealed class PlatformPermissionAuthorizationHandler(
    IPlatformPermissionResolver permissionResolver,
    IPlatformMfaAssurance mfaAssurance)
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
            if (!Guid.TryParse(context.User.FindFirstValue(SessionAuthenticationDefaults.SessionIdClaim), out var sessionId) ||
                !await mfaAssurance.IsSatisfiedAsync(userId, sessionId, cancellationToken))
            {
                throw new PlatformException(PlatformErrorCodes.MfaRequired, "تأیید MFA برای Backoffice الزامی است.");
            }
            context.Succeed(requirement);
        }
    }
}
