using OrdersApi.Application.DTOs;
using OrdersApi.Domain.Entities;

namespace OrdersApi.Application.Mappings;

public static class OrderMappingExtensions
{
    public static OrderResponse ToResponse(this Order order) => new()
    {
        Id = order.OrderId,
        CustomerName = order.CustomerName,
        Sku = order.Sku,
        Quantity = order.Quantity,
        Status = order.OrderStatus.ToString(),
        CreatedAt = order.CreatedAt
    };
}
