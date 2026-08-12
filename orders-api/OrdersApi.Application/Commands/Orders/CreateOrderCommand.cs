using MediatR;
using OrdersApi.Application.DTOs;

namespace OrdersApi.Application.Commands.Orders;

public class CreateOrderCommand : IRequest<OrderResponse>
{
    public required string CustomerName { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
}
