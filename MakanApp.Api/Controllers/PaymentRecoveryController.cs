using MakanApp.Api.Authentication;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/v1/commerce")]
public sealed class PaymentRecoveryController(IPaymentRecoveryService service) : ControllerBase
{
    private Guid Actor => AuthenticatedSession.GetUserId(User);
    private Guid Session => AuthenticatedSession.GetSessionId(User);

    [HttpGet("payment-attempts/{attemptId:guid}")]
    public async Task<ActionResult<PaymentAttemptDetailsDto>> GetAttempt(Guid attemptId, CancellationToken cancellationToken) =>
        Ok(await service.GetAttemptAsync(Actor, Session, attemptId, cancellationToken));

    [HttpPost("payment-attempts/{attemptId:guid}/verify"), EnableRateLimiting("payment")]
    public async Task<ActionResult<VerifyPaymentResult>> Verify(Guid attemptId, CancellationToken cancellationToken) =>
        Ok(await service.RecoverAsync(Actor, Session, attemptId, cancellationToken));

    [HttpGet("orders/{orderId:guid}/receipt")]
    public async Task<ActionResult<PaymentReceiptDto>> OrganizationReceipt(Guid orderId, CancellationToken cancellationToken) =>
        Ok(await service.GetReceiptAsync(Actor, Session, orderId, SubscriptionOwnerType.Organization, cancellationToken));

    [HttpGet("personal/orders/{orderId:guid}/receipt")]
    public async Task<ActionResult<PaymentReceiptDto>> PersonalReceipt(Guid orderId, CancellationToken cancellationToken) =>
        Ok(await service.GetReceiptAsync(Actor, Session, orderId, SubscriptionOwnerType.Individual, cancellationToken));
}
