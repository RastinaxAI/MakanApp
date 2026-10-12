namespace MakanApp.Application.Outbox;

public sealed record OutboxEnvelope(
    Guid MessageId,
    string MessageType,
    string Payload,
    DateTime OccurredAtUtc);

public interface IOutbox
{
    void Enqueue(OutboxEnvelope envelope);
    void EnqueueRange(IEnumerable<OutboxEnvelope> envelopes);
}
