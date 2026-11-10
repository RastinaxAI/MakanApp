using System.Globalization;
using MakanApp.Application.Commerce;
using MakanApp.Infrastructure.Commerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace MakanApp.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/integrations/v1/commerce/payments/zibal")]
public sealed class ZibalCallbackController(IPaymentRecoveryService payments, IOptions<ZibalOptions> options) : ControllerBase
{
    [HttpGet("callback"), EnableRateLimiting("payment-callback")]
    [ProducesResponseType(StatusCodes.Status303SeeOther)]
    public async Task<IActionResult> Callback(CancellationToken cancellationToken)
    {
        // رشته‌های خام Query وارد ModelState، پاسخ خطا یا لاگ نمی‌شوند.
        var query = Request.Query;
        var order = query["orderId"];
        var track = query["trackId"];
        var success = query["success"];
        var status = query["status"];
        if (order.Count != 1 || track.Count != 1 || success.Count != 1 || status.Count != 1
            || order[0]?.Length != 32 || !Guid.TryParseExact(order[0], "N", out var attemptId) || attemptId == Guid.Empty
            || track[0]?.Length is not (> 0 and <= 19)
            || !long.TryParse(track[0], NumberStyles.None, CultureInfo.InvariantCulture, out var trackId) || trackId <= 0
            || trackId.ToString(CultureInfo.InvariantCulture) != track[0]
            || success[0] is not ("0" or "1") || status[0]?.Length is not (> 0 and <= 4)
            || !int.TryParse(status[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var reportedStatus))
            throw new CommerceException(CommerceErrorCodes.PaymentInvalid, "پارامترهای Callback معتبر نیستند.");
        if (!options.Value.IsComplete) throw new CommerceException(CommerceErrorCodes.PaymentProviderUnavailable, "درگاه آماده نیست.");
        var result = await payments.HandleCallbackAsync(new(ZibalPaymentGateway.Name, attemptId, track[0]!, reportedStatus), cancellationToken);
        var location = QueryHelpers.AddQueryString(options.Value.ClientReturnUrl, new Dictionary<string, string?>
        {
            ["orderId"] = result.OrderId.ToString(), ["paymentAttemptId"] = result.PaymentAttemptId.ToString()
        });
        Response.Headers.Location = location;
        Response.Headers.CacheControl = "no-store";
        return StatusCode(StatusCodes.Status303SeeOther);
    }
}
