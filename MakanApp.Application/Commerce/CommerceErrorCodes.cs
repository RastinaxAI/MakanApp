namespace MakanApp.Application.Commerce;

public static class CommerceErrorCodes
{
    public const string SummaryNotAllowed = "COMMERCE_SUMMARY_NOT_ALLOWED";
    public const string SubscriptionRequired = "SUBSCRIPTION_REQUIRED";
    public const string SubscriptionInactive = "SUBSCRIPTION_INACTIVE";
    public const string EntitlementRequired = "ENTITLEMENT_REQUIRED";
    public const string QuotaRequired = "QUOTA_REQUIRED";
    public const string QuotaExceeded = "QUOTA_EXCEEDED";
    public const string UsageInvalid = "COMMERCE_USAGE_INVALID";
    public const string UsageConflict = "COMMERCE_USAGE_CONFLICT";
    public const string OrderInvalid = "ORDER_INVALID";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string OrderNotPayable = "ORDER_NOT_PAYABLE";
    public const string OrderIdempotencyConflict = "ORDER_IDEMPOTENCY_CONFLICT";
    public const string OrderManagementNotAllowed = "ORDER_MANAGEMENT_NOT_ALLOWED";
    public const string PlanNotPurchasable = "PLAN_NOT_PURCHASABLE";
    public const string PaymentInvalid = "PAYMENT_INVALID";
    public const string PaymentAttemptNotFound = "PAYMENT_ATTEMPT_NOT_FOUND";
    public const string PaymentIdempotencyConflict = "PAYMENT_IDEMPOTENCY_CONFLICT";
    public const string PaymentProviderUnavailable = "PAYMENT_PROVIDER_UNAVAILABLE";
    public const string PaymentPendingVerification = "PAYMENT_PENDING_VERIFICATION";
    public const string PaymentReferenceInvalid = "PAYMENT_REFERENCE_INVALID";
    public const string PaymentReferenceConflict = "PAYMENT_REFERENCE_CONFLICT";
    public const string PaymentAmountMismatch = "PAYMENT_AMOUNT_MISMATCH";
    public const string PaymentCurrencyMismatch = "PAYMENT_CURRENCY_MISMATCH";
    public const string PaymentRateLimited = "PAYMENT_RATE_LIMITED";
}
