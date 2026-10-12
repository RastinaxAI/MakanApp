using MakanApp.Application.Outbox;
using MakanApp.Infrastructure.Persistence;

namespace MakanApp.Infrastructure.Outbox;

public sealed class EfOutbox(MakanDbContext dbContext) : IOutbox
{
    public void Enqueue(OutboxEnvelope envelope)
    {
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            envelope.MessageId,
            envelope.MessageType,
            envelope.Payload,
            envelope.OccurredAtUtc));
    }

    public void EnqueueRange(IEnumerable<OutboxEnvelope> envelopes)
    {
        foreach (var envelope in envelopes)
        {
            Enqueue(envelope);
        }
    }
}
