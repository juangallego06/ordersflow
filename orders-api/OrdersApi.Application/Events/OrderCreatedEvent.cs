namespace OrdersApi.Application.Events;

public record OrderCreatedEvent(
    Guid EventId,
    string OrderId,
    string Sku,
    int Cantidad,
    DateTime OcurridoEn
);
