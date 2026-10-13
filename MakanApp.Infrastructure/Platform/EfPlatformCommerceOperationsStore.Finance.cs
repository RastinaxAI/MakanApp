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
        var ids = await orders
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenBy(order => order.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(order => order.Id)
            .ToArrayAsync(cancellationToken);
        var items = new List<PlatformOrderItem>(ids.Length);
        foreach (var id in ids)
        {
            var item = await GetOrderAsync(id, cancellationToken);
            if (item is not null)
            {
                items.Add(item);
            }
        }

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
