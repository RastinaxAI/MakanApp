namespace MakanApp.Application.Platform;

public interface IPlatformCommerceOperationsStore
{
    Task<PlatformPlanPage> GetPlansAsync(PlatformPlanQuery query, CancellationToken cancellationToken);
    Task<PlatformPlanDetail?> GetPlanAsync(Guid planId, CancellationToken cancellationToken);
    Task<PlatformPlanDetail> CreatePlanAsync(PlatformActorContext actor, CreatePlatformPlanCommand command, CancellationToken cancellationToken);
    Task<PlatformPlanVersionItem> CreatePlanVersionAsync(PlatformActorContext actor, Guid planId, CreatePlatformPlanVersionCommand command, CancellationToken cancellationToken);
    Task<PlatformPlanVersionItem> PublishPlanVersionAsync(PlatformActorContext actor, Guid planVersionId, byte[] expectedVersion, string reason, CancellationToken cancellationToken);
    Task<PlatformSubscriptionPage> GetSubscriptionsAsync(PlatformSubscriptionQuery query, CancellationToken cancellationToken);
    Task<PlatformSubscriptionOverrideResult> OverrideSubscriptionAsync(PlatformActorContext actor, Guid organizationId, OverridePlatformSubscriptionCommand command, CancellationToken cancellationToken);
    Task<PlatformOrderPage> GetOrdersAsync(PlatformOrderQuery query, CancellationToken cancellationToken);
    Task<PlatformOrderItem?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<PlatformNotificationOperations> GetNotificationOperationsAsync(DateTime nowUtc, CancellationToken cancellationToken);
    Task<PlatformJobOperations> GetJobOperationsAsync(CancellationToken cancellationToken);
    Task<PlatformSystemHealth> GetSystemHealthAsync(DateTime nowUtc, CancellationToken cancellationToken);
}

public interface IPlatformCommerceOperationsService
{
    Task<PlatformPlanPage> GetPlansAsync(PlatformPlanQuery query, CancellationToken cancellationToken);
    Task<PlatformPlanDetail> GetPlanAsync(Guid planId, CancellationToken cancellationToken);
    Task<PlatformPlanDetail> CreatePlanAsync(Guid actorUserId, Guid actorSessionId, CreatePlatformPlanCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformPlanVersionItem> CreatePlanVersionAsync(Guid actorUserId, Guid actorSessionId, Guid planId, CreatePlatformPlanVersionCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformPlanVersionItem> PublishPlanVersionAsync(Guid actorUserId, Guid actorSessionId, Guid planVersionId, PublishPlatformPlanVersionCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformSubscriptionPage> GetSubscriptionsAsync(PlatformSubscriptionQuery query, CancellationToken cancellationToken);
    Task<PlatformSubscriptionOverrideResult> OverrideSubscriptionAsync(Guid actorUserId, Guid actorSessionId, Guid organizationId, OverridePlatformSubscriptionCommand command, string correlationId, CancellationToken cancellationToken);
    Task<PlatformOrderPage> GetOrdersAsync(PlatformOrderQuery query, CancellationToken cancellationToken);
    Task<PlatformOrderItem> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<PlatformNotificationOperations> GetNotificationOperationsAsync(CancellationToken cancellationToken);
    Task<PlatformJobOperations> GetJobOperationsAsync(CancellationToken cancellationToken);
    Task<PlatformSystemHealth> GetSystemHealthAsync(CancellationToken cancellationToken);
}
