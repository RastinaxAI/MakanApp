using System.Diagnostics;
using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Commerce;

public sealed partial class OrderPaymentService
{
    public async Task<PaymentAttemptDetailsDto> GetAttemptAsync(Guid userId, Guid sessionId, Guid attemptId, CancellationToken cancellationToken)
    {
        var (attempt, order) = await ResolveOwnedAttemptAsync(userId, sessionId, attemptId, cancellationToken);
        return new(MapOrder(order), MapAttempt(attempt), await store.GetPaymentHistoryAsync(attemptId, cancellationToken));
    }

    public async Task<VerifyPaymentResult> RecoverAsync(Guid userId, Guid sessionId, Guid attemptId, CancellationToken cancellationToken)
    {
        var (attempt, _) = await ResolveOwnedAttemptAsync(userId, sessionId, attemptId, cancellationToken);
        return await VerifyPaymentAsync(new(attemptId, attempt.ProviderReference), cancellationToken);
    }

    public async Task<PaymentCallbackResult> HandleCallbackAsync(PaymentCallbackCommand command, CancellationToken cancellationToken)
    {
        var attempt = await store.GetPaymentAttemptAsync(command.PaymentAttemptId, cancellationToken);
        if (attempt is null || !attempt.Provider.Equals(command.Provider, StringComparison.Ordinal)
            || attempt.ProviderReference is not null && attempt.ProviderReference != command.ProviderReference)
            throw Error(CommerceErrorCodes.PaymentReferenceInvalid, "Callback پرداخت معتبر نیست.");
        await store.RecordCallbackAsync(attempt.Id, UtcNow(), command.ReportedStatus, Activity.Current?.TraceId.ToString(), cancellationToken);
        // حتی callback لغو/شکست فقط محرک بررسی است؛ کلاینت وضعیت را از API مجاز می‌خواند.
        await VerifyPaymentAsync(new(attempt.Id, command.ProviderReference), cancellationToken);
        return new(attempt.OrderId, attempt.Id);
    }

    public async Task<PaymentReceiptDto> GetReceiptAsync(Guid userId, Guid sessionId, Guid orderId, SubscriptionOwnerType ownerType,
        CancellationToken cancellationToken)
    {
        var organizationId = await ResolveOwnerOrganizationAsync(userId, sessionId, ownerType, cancellationToken);
        var order = await store.GetOrderAsync(organizationId, orderId, cancellationToken, organizationId.HasValue ? null : userId)
            ?? throw Error(CommerceErrorCodes.OrderNotFound, "سفارش پیدا نشد.");
        if (order.Status != OrderStatus.Paid || !order.PaidAtUtc.HasValue)
            throw Error(CommerceErrorCodes.PaymentPendingVerification, "برای سفارش پرداخت‌نشده رسید صادر نمی‌شود.");
        var paid = order.PaymentAttempts.SingleOrDefault(a => a.Status == PaymentAttemptStatus.Succeeded);
        return new(order.Id, order.PurchaserUserId, order.OrganizationId, order.PlanVersionId, order.ProductTitleSnapshot,
            order.Amount, Currency(order.Currency), order.PaidAtUtc.Value, order.FulfillmentState,
            paid?.Provider, paid?.ProviderReference, paid?.SettlementReference, paid?.ProviderPaidAtUtc,
            order.Purpose, order.BillingAnchorUtc, order.BillingPeriodIndex);
    }

    private async Task<(PaymentAttemptSnapshot Attempt, OrderPaymentSnapshot Order)> ResolveOwnedAttemptAsync(
        Guid userId, Guid sessionId, Guid attemptId, CancellationToken cancellationToken)
    {
        await actors.EnsureAsync(userId, sessionId, cancellationToken);
        var attempt = await store.GetPaymentAttemptAsync(attemptId, cancellationToken)
            ?? throw Error(CommerceErrorCodes.PaymentAttemptNotFound, "تلاش پرداخت پیدا نشد.");
        Guid? organizationId = null;
        if (attempt.OrganizationId.HasValue)
        {
            try { organizationId = await ResolveOwnerOrganizationAsync(userId, sessionId, SubscriptionOwnerType.Organization, cancellationToken); }
            catch (CommerceException error) when (error.Code == CommerceErrorCodes.OrderManagementNotAllowed)
            { throw Error(CommerceErrorCodes.PaymentAttemptNotFound, "تلاش پرداخت پیدا نشد."); }
            if (organizationId != attempt.OrganizationId) throw Error(CommerceErrorCodes.PaymentAttemptNotFound, "تلاش پرداخت پیدا نشد.");
        }
        var order = await store.GetOrderAsync(organizationId, attempt.OrderId, cancellationToken, organizationId.HasValue ? null : userId)
            ?? throw Error(CommerceErrorCodes.PaymentAttemptNotFound, "تلاش پرداخت پیدا نشد.");
        return (attempt, order);
    }
}
