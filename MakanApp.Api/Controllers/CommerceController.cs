using MakanApp.Api.Authentication;
using MakanApp.Application.Commerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/commerce")]
public sealed class CommerceController(
    ICommerceService commerceService,
    IOrderPaymentService orderPaymentService) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<ManagerCommerceSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagerCommerceSummaryDto>> GetSummary(
        CancellationToken cancellationToken) =>
        Ok(await commerceService.GetManagerSummaryAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken));

    [HttpPost("orders")]
    [EnableRateLimiting("payment")]
    [ProducesResponseType<CreateOrderResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<CreateOrderResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateOrderResult>> CreateOrder(
        [FromBody] CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var result = await orderPaymentService.CreateOrderAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken);
        return result.Replayed
            ? Ok(result)
            : CreatedAtAction(nameof(GetOrder), new { orderId = result.Order.OrderId }, result);
    }

    [HttpPost("orders/{orderId:guid}/payments")]
    [EnableRateLimiting("payment")]
    [ProducesResponseType<InitiatePaymentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<InitiatePaymentResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<InitiatePaymentResult>> InitiatePayment(
        Guid orderId,
        [FromBody] InitiatePaymentCommand command,
        CancellationToken cancellationToken)
    {
        var result = await orderPaymentService.InitiatePaymentAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            orderId,
            command,
            cancellationToken);
        return result.Replayed ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("orders/{orderId:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderDto>> GetOrder(
        Guid orderId,
        CancellationToken cancellationToken) =>
        Ok(await orderPaymentService.GetOrderAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            orderId,
            cancellationToken));

    [HttpGet("orders")]
    [ProducesResponseType<OrderListResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderListResult>> ListOrders(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await orderPaymentService.ListOrdersAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            limit,
            cancellationToken));
}
