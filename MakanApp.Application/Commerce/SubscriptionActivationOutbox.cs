using System.Text.Json;
using MakanApp.Application.Outbox;

namespace MakanApp.Application.Commerce;

public sealed record SubscriptionActivationRequested(
    Guid OrderId,
    Guid? OrganizationId,
    Guid PlanVersionId,
    DateTime PaidAtUtc,
    Guid? PurchaserUserId = null);

public static class SubscriptionActivationOutboxContract
{
    public const string MessageType = "commerce.subscription-activation-requested.v1";

    public static OutboxEnvelope Create(
        Guid orderId,
        Guid? organizationId,
        Guid planVersionId,
        DateTime paidAtUtc,
        Guid? purchaserUserId = null) =>
        new(
            Guid.NewGuid(),
            MessageType,
            JsonSerializer.Serialize(new SubscriptionActivationRequested(
                orderId,
                organizationId,
                planVersionId,
                paidAtUtc,
                purchaserUserId)),
            paidAtUtc);

    public static bool Matches(string messageType) =>
        string.Equals(messageType, MessageType, StringComparison.Ordinal);
}
