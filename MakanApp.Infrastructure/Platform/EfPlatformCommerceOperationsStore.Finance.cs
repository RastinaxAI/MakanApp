using MakanApp.Application.Platform;
using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Platform;

public sealed partial class EfPlatformCommerceOperationsStore
{
    public async Task<PlatformOrderPage> GetOrdersAsync(
        PlatformOrderQuery query,
        CancellationToken cancellationToken)
    {
        var orders = _dbContext.Orders.AsNoTracking();
        if (query.OrganizationId.HasValue)
        {
            orders = orders.Where(order => order.OrganizationId == query.OrganizationId.Value);
        }

        if (query.Status.HasValue)
        {
            orders = orders.Where(order => order.Status == query.Status.Value);
        }

        var totalCount = await orders.CountAsync(cancellationToken);
        var rows = await (
            from order in orders
            join candidateOrganization in _dbContext.Organizations.AsNoTracking()
                on order.OrganizationId equals (Guid?)candidateOrganization.Id into organizations
            from organization in organizations.DefaultIfEmpty()
            orderby order.CreatedAtUtc descending, order.Id
            select new
            {
                Order = order,
                OrganizationId = (Guid?)organization!.Id,
                OrganizationName = organization == null ? null : organization.Name
            })
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var orderIds = rows.Select(item => item.Order.Id).ToArray();
        var attemptRows = await _dbContext.PaymentAttempts.AsNoTracking()
            .Where(attempt => orderIds.Contains(attempt.OrderId))
            .OrderBy(attempt => attempt.CreatedAtUtc)
            .ThenBy(attempt => attempt.Id)
            .Select(attempt => new
            {
                attempt.OrderId,
                attempt.Id,
                attempt.Provider,
                attempt.Status,
                attempt.Amount,
                attempt.Currency,
                attempt.ProviderReference,
                attempt.CreatedAtUtc,
                attempt.VerifiedAtUtc,
                attempt.CompletedAtUtc,
                attempt.FailureCode
            })
            .ToArrayAsync(cancellationToken);
        var attempts = attemptRows.ToLookup(
            item => item.OrderId,
            item => new PlatformPaymentAttemptItem(
                item.Id,
                item.Provider,
                item.Status,
                item.Amount,
                item.Currency,
                MaskReference(item.ProviderReference),
                item.CreatedAtUtc,
                item.VerifiedAtUtc,
                item.CompletedAtUtc,
                item.FailureCode));
        var paymentRows = await _dbContext.Payments.AsNoTracking()
            .Where(payment => orderIds.Contains(payment.OrderId))
            .Select(payment => new
            {
                payment.OrderId,
                payment.Id,
                payment.PaymentAttemptId,
                payment.Provider,
                payment.ProviderReference,
                payment.Amount,
                payment.Currency,
                payment.PaidAtUtc
            })
            .ToArrayAsync(cancellationToken);
        var payments = paymentRows.ToDictionary(
            item => item.OrderId,
            item => new PlatformPaymentItem(
                item.Id,
                item.PaymentAttemptId,
                item.Provider,
                MaskReference(item.ProviderReference)!,
                item.Amount,
                item.Currency,
                item.PaidAtUtc));
        var items = rows.Select(row => new PlatformOrderItem(
            row.Order.Id,
            row.Order.PurchaserUserId,
            row.OrganizationId.HasValue
                ? new PlatformOrganizationContext(row.OrganizationId.Value, row.OrganizationName!)
                : null,
            row.Order.PlanVersionId,
            row.Order.Status,
            row.Order.Amount,
            row.Order.Currency,
            row.Order.CreatedAtUtc,
            row.Order.PaidAtUtc,
            row.Order.CancelledAtUtc,
            attempts[row.Order.Id].ToArray(),
            payments.GetValueOrDefault(row.Order.Id))).ToArray();

        return new PlatformOrderPage(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            _paymentGateway.ProviderName.Equals("Unavailable", StringComparison.OrdinalIgnoreCase)
                ? "Unavailable/ProviderDependencyPending"
                : "ProviderVerificationOnly");
    }

    public async Task<PlatformOrderItem?> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var row = await (
            from order in _dbContext.Orders.AsNoTracking()
            join candidateOrganization in _dbContext.Organizations.AsNoTracking()
                on order.OrganizationId equals (Guid?)candidateOrganization.Id into organizations
            from organization in organizations.DefaultIfEmpty()
            where order.Id == orderId
            select new
            {
                Order = order,
                OrganizationId = (Guid?)organization!.Id,
                OrganizationName = organization == null ? null : organization.Name
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var attempts = await _dbContext.PaymentAttempts.AsNoTracking()
            .Where(attempt => attempt.OrderId == orderId)
            .OrderBy(attempt => attempt.CreatedAtUtc)
            .ThenBy(attempt => attempt.Id)
            .Select(attempt => new PlatformPaymentAttemptItem(
                attempt.Id,
                attempt.Provider,
                attempt.Status,
                attempt.Amount,
                attempt.Currency,
                MaskReference(attempt.ProviderReference),
                attempt.CreatedAtUtc,
                attempt.VerifiedAtUtc,
                attempt.CompletedAtUtc,
                attempt.FailureCode))
            .ToArrayAsync(cancellationToken);
        var paymentRow = await _dbContext.Payments.AsNoTracking()
            .Where(payment => payment.OrderId == orderId)
            .Select(payment => new
            {
                payment.Id,
                payment.PaymentAttemptId,
                payment.Provider,
                payment.ProviderReference,
                payment.Amount,
                payment.Currency,
                payment.PaidAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);
        var payment = paymentRow is null
            ? null
            : new PlatformPaymentItem(
                paymentRow.Id,
                paymentRow.PaymentAttemptId,
                paymentRow.Provider,
                MaskReference(paymentRow.ProviderReference)!,
                paymentRow.Amount,
                paymentRow.Currency,
                paymentRow.PaidAtUtc);
        return new PlatformOrderItem(
            row.Order.Id,
            row.Order.PurchaserUserId,
            row.OrganizationId.HasValue
                ? new PlatformOrganizationContext(row.OrganizationId.Value, row.OrganizationName!)
                : null,
            row.Order.PlanVersionId,
            row.Order.Status,
            row.Order.Amount,
            row.Order.Currency,
            row.Order.CreatedAtUtc,
            row.Order.PaidAtUtc,
            row.Order.CancelledAtUtc,
            attempts,
            payment);
    }

    private static string? MaskReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        var visible = Math.Min(4, normalized.Length);
        return new string('*', Math.Max(4, normalized.Length - visible)) + normalized[^visible..];
    }
}
