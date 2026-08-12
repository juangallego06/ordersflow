namespace OrdersApi.Application.Events;

public record StockEvent(
    Guid EventId,
    string OrderId,
    string Sku,
    int Cantidad,
    DateTime OcurridoEn
);