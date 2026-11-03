using System.Security.Claims;
using System.Text.Encodings.Web;
using MakanApp.Api.Hosting;
using MakanApp.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MakanApp.Api.Authentication;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IIdentityService identityService,
    IProblemDetailsService problemDetailsService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.GetEndpoint()?.Metadata.GetMetadata<CoreHealthProbeMetadata>() is not null)
        {
            return AuthenticateResult.NoResult();
        }

        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var rawToken = authorization["Bearer ".Length..].Trim();
        var session = await identityService.AuthenticateAsync(rawToken, Context.RequestAborted);
        if (session is null)
        {
            return AuthenticateResult.Fail("Session is invalid, expired, or revoked.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new Claim(SessionAuthenticationDefaults.SessionIdClaim, session.SessionId.ToString())
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        await WriteFailureAsync(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Authentication required",
            Detail = "برای ادامه باید وارد شوید.",
            Type = "urn:makan:problem:auth-required",
            Extensions =
            {
                ["code"] = IdentityErrorCodes.AuthRequired
            }
        });
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        await WriteFailureAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden, Title = "Access denied",
            Detail = "مجوز جاری این عملیات را ندارید.", Type = "urn:makan:problem:auth-forbidden",
            Extensions = { ["code"] = MakanApp.Api.Errors.ApiErrorCodes.Forbidden }
        });
    }

    private async Task WriteFailureAsync(ProblemDetails problem)
    {
        Response.StatusCode = problem.Status!.Value;
        if (await problemDetailsService.TryWriteAsync(new ProblemDetailsContext { HttpContext = Context, ProblemDetails = problem })) return;
        // Accept فایل نباید code/correlation خطای ورود یا مجوز را حذف کند.
        problem.Extensions.TryAdd("traceId", System.Diagnostics.Activity.Current?.Id ?? Context.TraceIdentifier);
        problem.Extensions.TryAdd("correlationId", Context.TraceIdentifier);
        await Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json", cancellationToken: Context.RequestAborted);
    }
}
