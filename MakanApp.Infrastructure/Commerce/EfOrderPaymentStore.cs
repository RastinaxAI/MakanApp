using System.Data;
using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Commerce;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Commerce;

public sealed partial class EfOrderPaymentStore(
    MakanDbContext dbContext,
    IOutbox outbox,
    EfCommercialStore commerce,
    Microsoft.Extensions.Options.IOptions<CommercialOptions> commercialOptions,
    ILogger<EfOrderPaymentStore> logger) : IOrderPaymentStore
{
    public async Task<CreateOrderStoreResult> CreateOrderAsync(
        Guid purchaserUserId,
        Guid? organizationId,
        Guid planVersionId,
        Guid clientOperationId,
        byte[] requestPayloadHash,
        DateTime createdAtUtc,
        CancellationToken cancellationToken,
        CommercialOrderPurpose purpose = CommercialOrderPurpose.Purchase,
        Guid? targetSubscriptionId = null)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await commerce.LockOwnerAsync(organizationId.HasValue ? SubscriptionOwner.Organization(organizationId.Value) : SubscriptionOwner.Individual(purchaserUserId), cancellationToken);
        var existing = await dbContext.Orders
            .FromSqlInterpolated($"SELECT * FROM [commerce].[Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [PurchaserUserId] = {purchaserUserId} AND [ClientOperationId] = {clientOperationId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            var matches = FixedEquals(existing.RequestPayloadHash, requestPayloadHash)
                || !existing.HasCommercialTerms && purpose == CommercialOrderPurpose.Purchase && targetSubscriptionId is null
                    && existing.PlanVersionId == planVersionId && existing.OrganizationId == organizationId;
            var snapshot = matches
                ? await CreateSnapshotAsync(existing, cancellationToken)
                : null;
            await transaction.CommitAsync(cancellationToken);
            return new CreateOrderStoreResult(
                matches ? CreateOrderStoreStatus.Replayed : CreateOrderStoreStatus.IdempotencyConflict,
                snapshot);
        }

        var planVersion = await dbContext.PlanVersions
            .Include(version => version.Price).Include(version => version.Entitlements).Include(version => version.QuotaDefinitions)
            .SingleOrDefaultAsync(version => version.Id == planVersionId, cancellationToken);
        if (planVersion is null || !planVersion.IsPublished || planVersion.Price is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new CreateOrderStoreResult(CreateOrderStoreStatus.PlanNotPurchasable, null);
        }

        var order = Order.Create(
            purchaserUserId,
            organizationId,
            planVersion,
            clientOperationId,
            requestPayloadHash,
            createdAtUtc);
        var productTitle = await dbContext.Plans.Where(p => p.Id == planVersion.PlanId).Select(p => p.DisplayName).SingleAsync(cancellationToken);
        order.CaptureProductTitle(productTitle);
        await SetCommercialOrderTermsAsync(order, planVersion, purpose, targetSubscriptionId, createdAtUtc, cancellationToken);
        dbContext.Orders.Add(order);
        if (order.Amount == 0)
        {
            order.MarkPaid(createdAtUtc);
            outbox.Enqueue(SubscriptionActivationOutboxContract.Create(order.Id, order.OrganizationId, order.PlanVersionId, createdAtUtc, order.PurchaserUserId));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        var created = await CreateSnapshotAsync(order, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "سفارش {OrderId} برای سازمان {OrganizationId} و نسخه پلن {PlanVersionId} ایجاد شد.",
            order.Id,
            order.OrganizationId,
            order.PlanVersionId);
        return new CreateOrderStoreResult(CreateOrderStoreStatus.Created, created);
    }

    public async Task<OrderPaymentSnapshot?> GetOrderAsync(
        Guid? organizationId,
        Guid orderId,
        CancellationToken cancellationToken,
        Guid? individualUserId = null)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == orderId &&
                        item.OrganizationId == organizationId && (organizationId != null || item.PurchaserUserId == individualUserId),
                cancellationToken);
        return order is null ? null : await CreateSnapshotAsync(order, cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrderPaymentSnapshot>> ListOrdersAsync(
        Guid? organizationId,
        int limit,
        CancellationToken cancellationToken,
        Guid? individualUserId = null)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.OrganizationId == organizationId && (organizationId != null || order.PurchaserUserId == individualUserId))
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.Id)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        if (orders.Length == 0)
        {
            return [];
        }

        var orderIds = orders.Select(order => order.Id).ToArray();
        var attempts = await dbContext.PaymentAttempts
            .AsNoTracking()
            .Where(attempt => orderIds.Contains(attempt.OrderId))
            .OrderBy(attempt => attempt.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);
        var activatedOrders = await dbContext.Subscriptions
            .AsNoTracking()
            .Where(subscription =>
                subscription.SourceOrderId.HasValue &&
                orderIds.Contains(subscription.SourceOrderId.Value))
            .Select(subscription => subscription.SourceOrderId!.Value)
            .ToHashSetAsync(cancellationToken);
        activatedOrders.UnionWith(await dbContext.SubscriptionAmendments.Where(a => orderIds.Contains(a.OrderId))
            .Select(a => a.OrderId).ToArrayAsync(cancellationToken));

        return orders.Select(order => MapOrder(
            order,
            attempts.Where(attempt => attempt.OrderId == order.Id)
                .Select(attempt => MapAttempt(attempt, order))
                .ToArray(),
            activatedOrders.Contains(order.Id))).ToArray();
    }

    public async Task<CreatePaymentAttemptStoreResult> CreatePaymentAttemptAsync(
        Guid? organizationId,
        Guid orderId,
        string provider,
        Guid clientOperationId,
        byte[] requestPayloadHash,
        DateTime createdAtUtc,
        CancellationToken cancellationToken,
        Guid? individualUserId = null)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var order = await dbContext.Orders
            .FromSqlInterpolated($"SELECT * FROM [commerce].[Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {orderId} AND (({organizationId} IS NOT NULL AND [OrganizationId] = {organizationId}) OR ({organizationId} IS NULL AND [OrganizationId] IS NULL AND [PurchaserUserId] = {individualUserId}))")
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new CreatePaymentAttemptStoreResult(
                CreatePaymentAttemptStoreStatus.OrderNotFound,
                null,
                null);
        }

        var existing = await dbContext.PaymentAttempts
            .FromSqlInterpolated($"SELECT * FROM [commerce].[PaymentAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [OrderId] = {orderId} AND [ClientOperationId] = {clientOperationId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            var matches = FixedEquals(existing.RequestPayloadHash, requestPayloadHash)
                || FixedEquals(existing.RequestPayloadHash, SHA256.HashData(Encoding.UTF8.GetBytes($"payment|{orderId:N}|{existing.Provider}")));
            var snapshot = await CreateSnapshotAsync(order, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new CreatePaymentAttemptStoreResult(
                matches
                    ? CreatePaymentAttemptStoreStatus.Replayed
                    : CreatePaymentAttemptStoreStatus.IdempotencyConflict,
                matches ? snapshot : null,
                matches ? MapAttempt(existing, order) : null);
        }

        if (order.Status != OrderStatus.PendingPayment || order.TermsExpireAtUtc.HasValue && createdAtUtc >= order.TermsExpireAtUtc.Value)
        {
            await transaction.CommitAsync(cancellationToken);
            return new CreatePaymentAttemptStoreResult(
                CreatePaymentAttemptStoreStatus.OrderNotPayable,
                await CreateSnapshotAsync(order, cancellationToken),
                null);
        }

        if (await dbContext.PaymentAttempts.AnyAsync(a => a.OrderId == orderId && a.Status != PaymentAttemptStatus.Failed, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return new(CreatePaymentAttemptStoreStatus.UnresolvedAttempt, null, null);
        }

        var attempt = PaymentAttempt.Create(
            order,
            provider,
            clientOperationId,
            requestPayloadHash,
            createdAtUtc);
        dbContext.PaymentAttempts.Add(attempt);
        AddEvent(attempt.Id, PaymentAttemptEventType.Created, createdAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        var createdOrder = await CreateSnapshotAsync(order, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "تلاش پرداخت {PaymentAttemptId} برای سفارش {OrderId} و provider {Provider} ایجاد شد.",
            attempt.Id,
            order.Id,
            attempt.Provider);
        return new CreatePaymentAttemptStoreResult(
            CreatePaymentAttemptStoreStatus.Created,
            createdOrder,
            MapAttempt(attempt, order));
    }

    public async Task<bool> TryClaimPaymentInitiationAsync(
        Guid paymentAttemptId,
        Guid claimToken,
        DateTime nowUtc,
        DateTime claimedUntilUtc,
        CancellationToken cancellationToken)
    {
        var orderId = await FindPaymentOrderIdAsync(paymentAttemptId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockPaymentOrderAsync(orderId, cancellationToken);
        var attempt = await dbContext.PaymentAttempts
            .FromSqlInterpolated($"SELECT * FROM [commerce].[PaymentAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {paymentAttemptId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        var before = attempt.Status;
        var claimed = attempt.TryClaimInitiation(claimToken, nowUtc, claimedUntilUtc);
        if (claimed || before != attempt.Status)
            AddEvent(attempt.Id, claimed ? PaymentAttemptEventType.InitiationStarted : PaymentAttemptEventType.InitiationUncertain, nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    public async Task<CompletePaymentInitiationStoreResult> CompletePaymentInitiationAsync(
        Guid paymentAttemptId,
        Guid claimToken,
        PaymentGatewayCreationResult providerResult,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var orderId = await FindPaymentOrderIdAsync(paymentAttemptId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await LockPaymentOrderAsync(orderId, cancellationToken);
        var attempt = await dbContext.PaymentAttempts
            .FromSqlInterpolated($"SELECT * FROM [commerce].[PaymentAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {paymentAttemptId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null ||
            attempt.Status != PaymentAttemptStatus.Initiating ||
            attempt.InitiationClaimToken != claimToken)
        {
            await transaction.CommitAsync(cancellationToken);
            return new CompletePaymentInitiationStoreResult(
                CompletePaymentInitiationStoreStatus.StaleClaim,
                null,
                null);
        }

        if (await HasReferenceConflictAsync(
                attempt.Id,
                attempt.Provider,
                providerResult.ProviderReference,
                cancellationToken))
        {
            attempt.MarkInitiationUnknown(
                claimToken,
                CommerceErrorCodes.PaymentReferenceConflict,
                occurredAtUtc);
            AddEvent(attempt.Id, PaymentAttemptEventType.InitiationCompleted, occurredAtUtc, providerResult.ProviderResult,
                errorCode: CommerceErrorCodes.PaymentReferenceConflict);
            await dbContext.SaveChangesAsync(cancellationToken);
            var conflictOrder = await dbContext.Orders.SingleAsync(
                order => order.Id == attempt.OrderId,
                cancellationToken);
            var conflictSnapshot = await CreateSnapshotAsync(conflictOrder, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogWarning(
                "مرجع provider برای تلاش پرداخت {PaymentAttemptId} سفارش {OrderId} تکراری بود.",
                attempt.Id,
                conflictOrder.Id);
            return new CompletePaymentInitiationStoreResult(
                CompletePaymentInitiationStoreStatus.ReferenceConflict,
                conflictSnapshot,
                MapAttempt(attempt, conflictOrder));
        }

        switch (providerResult.Outcome)
        {
            case PaymentGatewayCreationOutcome.PendingVerification
                when !string.IsNullOrWhiteSpace(providerResult.ProviderReference) &&
                     !string.IsNullOrWhiteSpace(providerResult.PaymentUrl):
                attempt.MarkPendingVerification(
                    claimToken,
                    providerResult.ProviderReference,
                    providerResult.PaymentUrl,
                    occurredAtUtc);
                break;
            case PaymentGatewayCreationOutcome.Failed:
                attempt.MarkInitiationFailed(
                    claimToken,
                    SafeFailureCode(providerResult.FailureCode, "PAYMENT_PROVIDER_DECLINED"),
                    occurredAtUtc);
                break;
            case PaymentGatewayCreationOutcome.Unavailable:
                attempt.MarkInitiationFailed(
                    claimToken,
                    SafeFailureCode(providerResult.FailureCode, CommerceErrorCodes.PaymentProviderUnavailable),
                    occurredAtUtc);
                break;
            default:
                attempt.MarkInitiationUnknown(
                    claimToken,
                    SafeFailureCode(providerResult.FailureCode, CommerceErrorCodes.PaymentPendingVerification),
                    occurredAtUtc,
                    providerResult.ProviderReference);
                break;
        }

        AddEvent(attempt.Id, PaymentAttemptEventType.InitiationCompleted, occurredAtUtc, providerResult.ProviderResult, errorCode: attempt.FailureCode);
        await dbContext.SaveChangesAsync(cancellationToken);
        var order = await dbContext.Orders.SingleAsync(
            item => item.Id == attempt.OrderId,
            cancellationToken);
        var snapshot = await CreateSnapshotAsync(order, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "initiation تلاش پرداخت {PaymentAttemptId} سفارش {OrderId} با وضعیت {PaymentStatus} و provider {Provider} ثبت شد.",
            attempt.Id,
            order.Id,
            attempt.Status,
            attempt.Provider);
        return new CompletePaymentInitiationStoreResult(
            CompletePaymentInitiationStoreStatus.Completed,
            snapshot,
            MapAttempt(attempt, order));
    }

    public async Task<PaymentAttemptSnapshot?> GetPaymentAttemptAsync(
        Guid paymentAttemptId,
        CancellationToken cancellationToken)
    {
        var row = await (
            from attempt in dbContext.PaymentAttempts.AsNoTracking()
            join order in dbContext.Orders.AsNoTracking() on attempt.OrderId equals order.Id
            where attempt.Id == paymentAttemptId
            select new { Attempt = attempt, Order = order })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : MapAttempt(row.Attempt, row.Order);
    }

    public async Task<RecordPaymentVerificationStoreResult> RecordPaymentVerificationAsync(
        Guid paymentAttemptId,
        PaymentGatewayVerificationResult providerResult,
        DateTime verifiedAtUtc,
        OutboxEnvelope? subscriptionActivationMessage,
        CancellationToken cancellationToken)
    {
        var orderId = await FindPaymentOrderIdAsync(paymentAttemptId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await LockPaymentOrderAsync(orderId, cancellationToken);
        var attempt = await dbContext.PaymentAttempts
            .FromSqlInterpolated($"SELECT * FROM [commerce].[PaymentAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {paymentAttemptId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RecordPaymentVerificationStoreResult(
                RecordPaymentVerificationStoreStatus.AttemptNotFound,
                null,
                null);
        }

        foreach (var observation in providerResult.Observations)
            AddEvent(attempt.Id, observation.IsInquiry ? PaymentAttemptEventType.InquiryCompleted : PaymentAttemptEventType.VerificationCompleted,
                verifiedAtUtc, observation.Result, observation.Status, providerResult.FailureCode);

        var order = await dbContext.Orders
            .FromSqlInterpolated($"SELECT * FROM [commerce].[Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {attempt.OrderId}")
            .SingleAsync(cancellationToken);
        var existingPayment = await dbContext.Payments
            .FromSqlInterpolated($"SELECT * FROM [commerce].[Payments] WITH (UPDLOCK, HOLDLOCK) WHERE [OrderId] = {order.Id}")
            .SingleOrDefaultAsync(cancellationToken);
        if (existingPayment is not null ||
            attempt.Status == PaymentAttemptStatus.Succeeded && order.Status == OrderStatus.Paid)
        {
            AddEvent(attempt.Id, PaymentAttemptEventType.SettlementReplayed, verifiedAtUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            var existingSnapshot = await CreateSnapshotAsync(order, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation(
                "settlement تکراری سفارش {OrderId} و تلاش پرداخت {PaymentAttemptId} بدون اثر جدید بازگردانده شد.",
                order.Id,
                attempt.Id);
            return new RecordPaymentVerificationStoreResult(
                RecordPaymentVerificationStoreStatus.AlreadySettled,
                existingSnapshot,
                MapAttempt(attempt, order));
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            var nonPayable = await CreateSnapshotAsync(order, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RecordPaymentVerificationStoreResult(
                RecordPaymentVerificationStoreStatus.OrderNotPayable,
                nonPayable,
                MapAttempt(attempt, order));
        }

        if (ReferenceDiffers(attempt.ProviderReference, providerResult.ProviderReference) ||
            await HasReferenceConflictAsync(
                attempt.Id,
                attempt.Provider,
                providerResult.ProviderReference,
                cancellationToken))
        {
            attempt.MarkVerificationUnknown(
                null,
                CommerceErrorCodes.PaymentReferenceConflict,
                verifiedAtUtc);
            return await CompleteVerificationAsync(
                transaction,
                order,
                attempt,
                RecordPaymentVerificationStoreStatus.ReferenceConflict,
                verifiedAtUtc,
                cancellationToken);
        }

        if (providerResult.Outcome == PaymentGatewayVerificationOutcome.Failed)
        {
            attempt.MarkVerificationFailed(
                providerResult.ProviderReference,
                SafeFailureCode(providerResult.FailureCode, "PAYMENT_PROVIDER_DECLINED"),
                verifiedAtUtc);
            return await CompleteVerificationAsync(
                transaction,
                order,
                attempt,
                RecordPaymentVerificationStoreStatus.Failed,
                verifiedAtUtc,
                cancellationToken);
        }

        if (providerResult.Outcome != PaymentGatewayVerificationOutcome.Succeeded ||
            !providerResult.VerifiedAmount.HasValue ||
            !providerResult.VerifiedCurrency.HasValue ||
            string.IsNullOrWhiteSpace(providerResult.ProviderReference))
        {
            attempt.MarkVerificationUnknown(
                providerResult.ProviderReference,
                SafeFailureCode(providerResult.FailureCode, CommerceErrorCodes.PaymentPendingVerification),
                verifiedAtUtc);
            return await CompleteVerificationAsync(
                transaction,
                order,
                attempt,
                RecordPaymentVerificationStoreStatus.Unknown,
                verifiedAtUtc,
                cancellationToken);
        }

        if (providerResult.VerifiedAmount.Value != order.Amount)
        {
            attempt.MarkVerificationUnknown(
                providerResult.ProviderReference,
                CommerceErrorCodes.PaymentAmountMismatch,
                verifiedAtUtc);
            return await CompleteVerificationAsync(
                transaction,
                order,
                attempt,
                RecordPaymentVerificationStoreStatus.AmountMismatch,
                verifiedAtUtc,
                cancellationToken);
        }

        if (providerResult.VerifiedCurrency.Value != order.Currency)
        {
            attempt.MarkVerificationUnknown(
                providerResult.ProviderReference,
                CommerceErrorCodes.PaymentCurrencyMismatch,
                verifiedAtUtc);
            return await CompleteVerificationAsync(
                transaction,
                order,
                attempt,
                RecordPaymentVerificationStoreStatus.CurrencyMismatch,
                verifiedAtUtc,
                cancellationToken);
        }

        attempt.MarkSucceeded(providerResult.ProviderReference, verifiedAtUtc, providerResult.SettlementReference, providerResult.ProviderPaidAtUtc);
        order.MarkPaid(verifiedAtUtc);
        dbContext.Payments.Add(Payment.Create(attempt, verifiedAtUtc));
        if (order.OrganizationId.HasValue || order.HasCommercialTerms)
        {
            if (subscriptionActivationMessage is null)
            {
                throw new InvalidOperationException("پیام durable فعال‌سازی اشتراک برای سفارش سازمانی الزامی است.");
            }

            outbox.Enqueue(subscriptionActivationMessage);
        }

        return await CompleteVerificationAsync(
            transaction,
            order,
            attempt,
            RecordPaymentVerificationStoreStatus.Settled,
            verifiedAtUtc,
            cancellationToken);
    }

    private async Task<RecordPaymentVerificationStoreResult> CompleteVerificationAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        Order order,
        PaymentAttempt attempt,
        RecordPaymentVerificationStoreStatus status,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        AddEvent(attempt.Id, PaymentAttemptEventType.VerificationCompleted, occurredAtUtc,
            errorCode: attempt.FailureCode);
        await dbContext.SaveChangesAsync(cancellationToken);
        var snapshot = await CreateSnapshotAsync(order, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (status is RecordPaymentVerificationStoreStatus.AmountMismatch or
            RecordPaymentVerificationStoreStatus.CurrencyMismatch or
            RecordPaymentVerificationStoreStatus.ReferenceConflict)
        {
            logger.LogWarning(
                "verification تلاش پرداخت {PaymentAttemptId} سفارش {OrderId} با نتیجه {VerificationStatus} و provider {Provider} ثبت شد.",
                attempt.Id,
                order.Id,
                status,
                attempt.Provider);
        }
        else
        {
            logger.LogInformation(
                "verification تلاش پرداخت {PaymentAttemptId} سفارش {OrderId} با نتیجه {VerificationStatus} و provider {Provider} ثبت شد.",
                attempt.Id,
                order.Id,
                status,
                attempt.Provider);
        }

        return new RecordPaymentVerificationStoreResult(
            status,
            snapshot,
            MapAttempt(attempt, order));
    }

    private async Task<OrderPaymentSnapshot> CreateSnapshotAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var attempts = await dbContext.PaymentAttempts
            .AsNoTracking()
            .Where(attempt => attempt.OrderId == order.Id)
            .OrderBy(attempt => attempt.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);
        var activated = order.Status == OrderStatus.Paid &&
                        (await dbContext.Subscriptions
                            .AsNoTracking()
                            .AnyAsync(subscription => subscription.SourceOrderId == order.Id, cancellationToken)
                         || await dbContext.SubscriptionAmendments.AnyAsync(a => a.OrderId == order.Id, cancellationToken));
        return MapOrder(
            order,
            attempts.Select(attempt => MapAttempt(attempt, order)).ToArray(),
            activated);
    }

    private static OrderPaymentSnapshot MapOrder(
        Order order,
        IReadOnlyCollection<PaymentAttemptSnapshot> attempts,
        bool activated) =>
        new(
            order.Id,
            order.PurchaserUserId,
            order.OrganizationId,
            order.PlanVersionId,
            order.Status,
            order.Amount,
            order.Currency,
            order.CreatedAtUtc,
            order.PaidAtUtc,
            FulfillmentState(order, activated),
            attempts)
        {
            ProductTitleSnapshot = order.ProductTitleSnapshot, Purpose = order.Purpose,
            BillingAnchorUtc = order.BillingAnchorUtc, BillingPeriodIndex = order.BillingPeriodIndex
        };

    private static PaymentAttemptSnapshot MapAttempt(PaymentAttempt attempt, Order order) =>
        new(
            attempt.Id,
            attempt.OrderId,
            order.OrganizationId,
            order.PlanVersionId,
            attempt.Provider,
            attempt.Status,
            attempt.Amount,
            attempt.Currency,
            attempt.ProviderReference,
            attempt.PaymentUrl,
            attempt.CreatedAtUtc,
            attempt.CompletedAtUtc,
            order.PurchaserUserId)
        {
            SettlementReference = attempt.SettlementReference, FailureCode = attempt.FailureCode,
            CallbackReceivedAtUtc = attempt.CallbackReceivedAtUtc, ProviderPaidAtUtc = attempt.ProviderPaidAtUtc,
            VerifiedAtUtc = attempt.VerifiedAtUtc
        };

    private static OrderFulfillmentState FulfillmentState(Order order, bool activated)
    {
        if (order.Status != OrderStatus.Paid)
        {
            return OrderFulfillmentState.NotStarted;
        }

        if (!order.OrganizationId.HasValue && !order.HasCommercialTerms)
        {
            return OrderFulfillmentState.ProvisioningPending;
        }

        return activated
            ? OrderFulfillmentState.SubscriptionActivated
            : OrderFulfillmentState.SubscriptionActivationPending;
    }

    private async Task<bool> HasReferenceConflictAsync(
        Guid attemptId,
        string provider,
        string? providerReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
        {
            return false;
        }

        var normalized = providerReference.Trim();
        return await dbContext.PaymentAttempts
            .AsNoTracking()
            .AnyAsync(
                attempt => attempt.Id != attemptId &&
                           attempt.Provider == provider &&
                           attempt.ProviderReference == normalized,
                cancellationToken);
    }

    private static bool ReferenceDiffers(string? stored, string? verified) =>
        !string.IsNullOrWhiteSpace(stored) &&
        !string.IsNullOrWhiteSpace(verified) &&
        !stored.Equals(verified.Trim(), StringComparison.Ordinal);

    private static bool FixedEquals(byte[] left, byte[] right) =>
        left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);

    private static string SafeFailureCode(string? value, string fallback)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ||
               normalized.Length > PaymentAttempt.MaximumFailureCodeLength
            ? fallback
            : normalized;
    }
}
