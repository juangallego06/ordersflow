using MediatR;
using OrdersApi.Application.DTOs;

namespace OrdersApi.Application.Queries.Orders;

public class GetOrderByIdQuery : IRequest<OrderResponse?>
{
    public required string OrderId { get; init; }
}