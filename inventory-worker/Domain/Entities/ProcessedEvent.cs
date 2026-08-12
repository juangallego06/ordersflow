namespace InventoryWorker.Domain.Entities;

public class ProcessedEvent
{
    public Guid EventId { get; private set; }

    public DateTime ProcessedAt { get; private set; }

    private ProcessedEvent() { }

    public ProcessedEvent(Guid eventId)
    {
        EventId = eventId;
    }

}
