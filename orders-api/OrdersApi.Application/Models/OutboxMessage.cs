namespace OrdersApi.Application.Models;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; }
    public string Payload { get; private set; }
    public DateTime OcurredOn { get; private set; }
    public DateTime? ProcessedOn { get; private set; }

    // Constructor privado sin parámetros: necesario para EF Core.
    private OutboxMessage() { }

    private OutboxMessage(string eventType, string payload)
    {
        Id = Guid.NewGuid();
        EventType = eventType;
        Payload = payload;
        OcurredOn = DateTime.UtcNow;
        ProcessedOn = null;
    }

    public static OutboxMessage Create(string eventType, string payload)
    {
        if(string.IsNullOrWhiteSpace(eventType)) 
            throw new ArgumentException("El tipo de evento no puede estar vacío", nameof(eventType));

        if(string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("El payload no puede estar vacío", nameof(payload));

        return new OutboxMessage(eventType, payload);
    }

    public void MarkAsProcessed()
    {
        if (ProcessedOn is not null)
            throw new InvalidOperationException("Este mensaje ya fue marcado como procesado");

        ProcessedOn = DateTime.UtcNow;
    }
}
