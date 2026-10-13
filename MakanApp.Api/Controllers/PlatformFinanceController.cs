using MakanApp.Application.Platform;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Authorize(Policy = PlatformPermissionKeys.FinanceRead)]
[Route("api/admin/v1/finance/orders")]
public sealed class PlatformFinanceController(
    IPlatformCommerceOperationsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlatformOrderPage>> GetOrders(
        [FromQuery] Guid? organizationId,
        [FromQuery] OrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetOrdersAsync(
            new PlatformOrderQuery(organizationId, status, page, pageSize),
            cancellationToken));

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<PlatformOrderItem>> GetOrder(
        Guid orderId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetOrderAsync(orderId, cancellationToken));
}
