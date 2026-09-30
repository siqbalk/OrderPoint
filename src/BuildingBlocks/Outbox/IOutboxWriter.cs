using BuildingBlocks.Messaging;

namespace BuildingBlocks.Outbox;

/// <summary>
/// Implemented by each module's DbContext. Enqueues an outbox row using the
/// SAME change tracker / transaction as the business change that raised the event —
/// this is what makes the write atomic without a distributed transaction.
/// </summary>
public interface IOutboxWriter
{
    void Enqueue(IIntegrationEvent integrationEvent);
}
