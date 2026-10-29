using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Commerce;

public sealed class OrderPaymentService(
    IAccessContextResolver accessContextResolver,
    CommercialActorAccess actors,
    IOrderPaymentStore store,
    IPaymentGateway paymentGateway,
    PaymentOptions options,
    TimeProvider timeProvider) : IOrderPaymentService, IPaymentVerificationService
{
    public async Task<CommercialQuoteDto> QuoteAsync(Guid userId, Guid sessionId, CreateOrderCommand command, CancellationToken cancellationToken,
        SubscriptionOwnerType ownerType = SubscriptionOwnerType.Organization)
    {
        if (command.PlanVersionId == Guid.Empty || !Enum.IsDefined(command.Purpose)) throw Error(CommerceErrorCodes.OrderInvalid, "درخواست پیش‌فاکتور معتبر نیست.");
        var organizationId = await ResolveOwnerOrganizationAsync(userId, sessionId, ownerType, cancellationToken);
        return await store.QuoteAsync(userId, organizationId, command, UtcNow(), cancellationToken);
    }
    public async Task<CreateOrderResult> CreateOrderAsync(
        Guid userId,
        Guid sessionId,
        CreateOrderCommand command,
        CancellationToken cancellationToken,
        SubscriptionOwnerType ownerType = SubscriptionOwnerType.Organization)
    {
        if (command.PlanVersionId == Guid.Empty || command.ClientOperationId == Guid.Empty)
        {
            throw Error(CommerceErrorCodes.OrderInvalid, "شناسه نسخه پلن و عملیات سفارش الزامی است.");
        }

        var organizationId = await ResolveOwnerOrganizationAsync(
            userId,
            sessionId,
            ownerType,
            cancellationToken);
        var result = await store.CreateOrderAsync(
            userId,
            organizationId,
            command.PlanVersionId,
            command.ClientOperationId,
            Hash($"order|{ownerType}|{organizationId:N}|{command.PlanVersionId:N}|{command.Purpose}|{command.SubscriptionId:N}"),
            UtcNow(),
            cancellationToken,
            command.Purpose, command.SubscriptionId);
        return result.Status switch
        {
            CreateOrderStoreStatus.Created or CreateOrderStoreStatus.Replayed =>
                new CreateOrderResult(
                    MapOrder(result.Order!),
                    result.Status == CreateOrderStoreStatus.Replayed),
            CreateOrderStoreStatus.IdempotencyConflict => throw Error(
                CommerceErrorCodes.OrderIdempotencyConflict,
                "شناسه عملیات سفارش قبلاً با payload دیگری استفاده شده است."),
            CreateOrderStoreStatus.PlanNotPurchasable => throw Error(
                CommerceErrorCodes.PlanNotPurchasable,
                "نسخه پلن منتشر، قیمت‌گذاری‌شده و قابل خرید نیست."),
            _ => throw Error(CommerceErrorCodes.OrderInvalid, "نتیجه ایجاد سفارش معتبر نیست.")
        };
    }

    public async Task<InitiatePaymentResult> InitiatePaymentAsync(
        Guid userId,
        Guid sessionId,
        Guid orderId,
        InitiatePaymentCommand command,
        CancellationToken cancellationToken,
        SubscriptionOwnerType ownerType = SubscriptionOwnerType.Organization)
    {
        if (orderId == Guid.Empty || command.ClientOperationId == Guid.Empty)
        {
            throw Error(CommerceErrorCodes.PaymentInvalid, "شناسه سفارش و عملیات پرداخت الزامی است.");
        }

        var organizationId = await ResolveOwnerOrganizationAsync(
            userId,
            sessionId,
            ownerType,
            cancellationToken);
        var created = await store.CreatePaymentAttemptAsync(
            organizationId,
            orderId,
            paymentGateway.ProviderName,
            command.ClientOperationId,
            Hash($"payment|{orderId:N}|{paymentGateway.ProviderName}"),
            UtcNow(),
            cancellationToken,
            organizationId.HasValue ? null : userId);
        if (created.Status == CreatePaymentAttemptStoreStatus.IdempotencyConflict)
        {
            throw Error(
                CommerceErrorCodes.PaymentIdempotencyConflict,
                "شناسه عملیات پرداخت قبلاً برای payload دیگری استفاده شده است.");
        }

        if (created.Status == CreatePaymentAttemptStoreStatus.OrderNotFound)
        {
            throw Error(CommerceErrorCodes.OrderNotFound, "سفارش پیدا نشد.");
        }

        if (created.Status == CreatePaymentAttemptStoreStatus.OrderNotPayable)
        {
            throw Error(CommerceErrorCodes.OrderNotPayable, "سفارش در وضعیت قابل پرداخت نیست.");
        }

        var attempt = created.Attempt!;
        var replayed = created.Status == CreatePaymentAttemptStoreStatus.Replayed;
        var claimToken = Guid.NewGuid();
        var nowUtc = UtcNow();
        var claimed = await store.TryClaimPaymentInitiationAsync(
            attempt.Id,
            claimToken,
            nowUtc,
            nowUtc.Add(options.InitiationClaimLease),
            cancellationToken);
        if (!claimed)
        {
            var current = await store.GetOrderAsync(organizationId, orderId, cancellationToken, organizationId.HasValue ? null : userId)
                ?? throw Error(CommerceErrorCodes.OrderNotFound, "سفارش پیدا نشد.");
            var currentAttempt = current.PaymentAttempts.Single(item => item.Id == attempt.Id);
            return new InitiatePaymentResult(
                MapOrder(current),
                MapAttempt(currentAttempt),
                true);
        }

        PaymentGatewayCreationResult providerResult;
        try
        {
            providerResult = await paymentGateway.CreatePaymentAsync(
                new PaymentGatewayCreationRequest(
                    attempt.Id,
                    attempt.OrderId,
                    attempt.Amount,
                    attempt.Currency,
                    $"MakanApp order {attempt.OrderId:N}"),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await PersistUnknownInitiationAsync(attempt.Id, claimToken, CancellationToken.None);
            throw;
        }
        catch
        {
            providerResult = new PaymentGatewayCreationResult(
                PaymentGatewayCreationOutcome.Unknown,
                null,
                null,
                CommerceErrorCodes.PaymentPendingVerification);
        }

        var completed = await store.CompletePaymentInitiationAsync(
            attempt.Id,
            claimToken,
            providerResult,
            UtcNow(),
            cancellationToken);
        if (completed.Status == CompletePaymentInitiationStoreStatus.ReferenceConflict)
        {
            throw Error(
                CommerceErrorCodes.PaymentReferenceConflict,
                "مرجع provider قبلاً برای تلاش پرداخت دیگری ثبت شده است.");
        }

        if (providerResult.Outcome == PaymentGatewayCreationOutcome.Unavailable)
        {
            throw Error(
                CommerceErrorCodes.PaymentProviderUnavailable,
                "درگاه پرداخت پیکربندی یا در دسترس نیست.");
        }

        if (completed.Status == CompletePaymentInitiationStoreStatus.StaleClaim)
        {
            var current = await store.GetOrderAsync(organizationId, orderId, cancellationToken, organizationId.HasValue ? null : userId)
                ?? throw Error(CommerceErrorCodes.OrderNotFound, "سفارش پیدا نشد.");
            var currentAttempt = current.PaymentAttempts.Single(item => item.Id == attempt.Id);
            return new InitiatePaymentResult(MapOrder(current), MapAttempt(currentAttempt), true);
        }

        return new InitiatePaymentResult(
            MapOrder(completed.Order!),
            MapAttempt(completed.Attempt!),
            replayed);
    }

    public async Task<OrderDto> GetOrderAsync(
        Guid userId,
        Guid sessionId,
        Guid orderId,
        CancellationToken cancellationToken,
        SubscriptionOwnerType ownerType = SubscriptionOwnerType.Organization)
    {
        var organizationId = await ResolveOwnerOrganizationAsync(userId, sessionId, ownerType, cancellationToken);
        var order = await store.GetOrderAsync(organizationId, orderId, cancellationToken, organizationId.HasValue ? null : userId);
        return order is null
            ? throw Error(CommerceErrorCodes.OrderNotFound, "سفارش پیدا نشد.")
            : MapOrder(order);
    }

    public async Task<OrderListResult> ListOrdersAsync(
        Guid userId,
        Guid sessionId,
        int limit,
        CancellationToken cancellationToken,
        SubscriptionOwnerType ownerType = SubscriptionOwnerType.Organization)
    {
        if (limit is < 1 or > 100)
        {
            throw Error(CommerceErrorCodes.OrderInvalid, "limit باید بین ۱ و ۱۰۰ باشد.");
        }

        var organizationId = await ResolveOwnerOrganizationAsync(userId, sessionId, ownerType, cancellationToken);
        var orders = await store.ListOrdersAsync(organizationId, limit, cancellationToken, organizationId.HasValue ? null : userId);
        return new OrderListResult(orders.Select(MapListItem).ToArray());
    }

    public async Task<VerifyPaymentResult> VerifyPaymentAsync(
        VerifyPaymentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.PaymentAttemptId == Guid.Empty)
        {
            throw Error(CommerceErrorCodes.PaymentInvalid, "شناسه تلاش پرداخت الزامی است.");
        }

        var attempt = await store.GetPaymentAttemptAsync(command.PaymentAttemptId, cancellationToken)
            ?? throw Error(CommerceErrorCodes.PaymentAttemptNotFound, "تلاش پرداخت پیدا نشد.");
        if (!attempt.Provider.Equals(paymentGateway.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            throw Error(CommerceErrorCodes.PaymentProviderUnavailable, "provider این تلاش پرداخت در دسترس نیست.");
        }

        var observedReference = NormalizeOptional(command.ProviderReference);
        if (attempt.ProviderReference is not null &&
            observedReference is not null &&
            !attempt.ProviderReference.Equals(observedReference, StringComparison.Ordinal))
        {
            throw Error(CommerceErrorCodes.PaymentReferenceInvalid, "مرجع callback با تلاش پرداخت تطبیق ندارد.");
        }

        PaymentGatewayVerificationResult providerResult;
        try
        {
            providerResult = await paymentGateway.VerifyPaymentAsync(
                new PaymentGatewayVerificationRequest(
                    attempt.Id,
                    attempt.OrderId,
                    attempt.Amount,
                    attempt.Currency,
                    observedReference ?? attempt.ProviderReference),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await PersistUnknownVerificationAsync(attempt, CancellationToken.None);
            throw;
        }
        catch
        {
            providerResult = UnknownVerification(attempt.ProviderReference);
        }

        if (providerResult.Outcome == PaymentGatewayVerificationOutcome.Unavailable)
        {
            throw Error(
                CommerceErrorCodes.PaymentProviderUnavailable,
                "امکان بررسی server-to-server پرداخت وجود ندارد.");
        }

        var verifiedAtUtc = UtcNow();
        var activation = SubscriptionActivationOutboxContract.Create(
                attempt.OrderId,
                attempt.OrganizationId,
                attempt.PlanVersionId,
                verifiedAtUtc, attempt.PurchaserUserId);
        var stored = await store.RecordPaymentVerificationAsync(
            attempt.Id,
            providerResult,
            verifiedAtUtc,
            activation,
            cancellationToken);
        if (stored.Status == RecordPaymentVerificationStoreStatus.AttemptNotFound)
        {
            throw Error(CommerceErrorCodes.PaymentAttemptNotFound, "تلاش پرداخت پیدا نشد.");
        }

        if (stored.Status == RecordPaymentVerificationStoreStatus.OrderNotPayable)
        {
            throw Error(CommerceErrorCodes.OrderNotPayable, "سفارش در وضعیت قابل تسویه نیست.");
        }

        return new VerifyPaymentResult(
            MapVerificationStatus(stored.Status),
            MapOrder(stored.Order!),
            MapAttempt(stored.Attempt!));
    }

    private async Task<Guid?> ResolveOwnerOrganizationAsync(
        Guid userId,
        Guid sessionId,
        SubscriptionOwnerType ownerType,
        CancellationToken cancellationToken)
    {
        await actors.EnsureAsync(userId, sessionId, cancellationToken);
        if (ownerType == SubscriptionOwnerType.Individual) return null;
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (ownerType != SubscriptionOwnerType.Organization || context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            context.ActiveRole != OrganizationRole.Manager)
        {
            throw Error(
                CommerceErrorCodes.OrderManagementNotAllowed,
                "عملیات سفارش و پرداخت فقط برای مدیر فعال سازمان مجاز است.");
        }

        return context.OrganizationId.Value;
    }

    private async Task PersistUnknownInitiationAsync(
        Guid attemptId,
        Guid claimToken,
        CancellationToken cancellationToken) =>
        _ = await store.CompletePaymentInitiationAsync(
            attemptId,
            claimToken,
            new PaymentGatewayCreationResult(
                PaymentGatewayCreationOutcome.Unknown,
                null,
                null,
                CommerceErrorCodes.PaymentPendingVerification),
            UtcNow(),
            cancellationToken);

    private async Task PersistUnknownVerificationAsync(
        PaymentAttemptSnapshot attempt,
        CancellationToken cancellationToken) =>
        _ = await store.RecordPaymentVerificationAsync(
            attempt.Id,
            UnknownVerification(attempt.ProviderReference),
            UtcNow(),
            null,
            cancellationToken);

    private static PaymentGatewayVerificationResult UnknownVerification(string? providerReference) =>
        new(
            PaymentGatewayVerificationOutcome.Unknown,
            providerReference,
            null,
            null,
            CommerceErrorCodes.PaymentPendingVerification);

    private static OrderDto MapOrder(OrderPaymentSnapshot order) =>
        new(
            order.Id,
            order.OrganizationId,
            order.PlanVersionId,
            order.Status,
            order.Amount,
            Currency(order.Currency),
            order.CreatedAtUtc,
            order.PaidAtUtc,
            order.FulfillmentState,
            order.PaymentAttempts.Select(MapAttempt).ToArray());

    private static OrderListItemDto MapListItem(OrderPaymentSnapshot order) =>
        new(
            order.Id,
            order.PlanVersionId,
            order.Status,
            order.Amount,
            Currency(order.Currency),
            order.CreatedAtUtc,
            order.PaidAtUtc,
            order.FulfillmentState,
            order.PaymentAttempts.OrderByDescending(item => item.CreatedAtUtc).FirstOrDefault()?.Status);

    private static PaymentAttemptDto MapAttempt(PaymentAttemptSnapshot attempt) =>
        new(
            attempt.Id,
            attempt.Provider,
            attempt.Status,
            attempt.Amount,
            Currency(attempt.Currency),
            attempt.ProviderReference,
            attempt.PaymentUrl,
            attempt.CreatedAtUtc,
            attempt.CompletedAtUtc);

    private static PaymentVerificationStatus MapVerificationStatus(
        RecordPaymentVerificationStoreStatus status) => status switch
        {
            RecordPaymentVerificationStoreStatus.Settled => PaymentVerificationStatus.Settled,
            RecordPaymentVerificationStoreStatus.AlreadySettled => PaymentVerificationStatus.AlreadySettled,
            RecordPaymentVerificationStoreStatus.Failed => PaymentVerificationStatus.Failed,
            RecordPaymentVerificationStoreStatus.Unknown => PaymentVerificationStatus.Unknown,
            RecordPaymentVerificationStoreStatus.AmountMismatch => PaymentVerificationStatus.AmountMismatch,
            RecordPaymentVerificationStoreStatus.CurrencyMismatch => PaymentVerificationStatus.CurrencyMismatch,
            RecordPaymentVerificationStoreStatus.ReferenceConflict => PaymentVerificationStatus.ReferenceConflict,
            _ => throw new InvalidOperationException("وضعیت verification قابل نگاشت نیست.")
        };

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private static string Currency(CurrencyCode currency) => currency switch
    {
        CurrencyCode.Irr => "IRR",
        _ => throw new InvalidOperationException("واحد پول پشتیبانی نمی‌شود.")
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CommerceException Error(string code, string message) => new(code, message);

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
