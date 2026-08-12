using MediatR;
using OrdersApi.Application.DTOs;

namespace OrdersApi.Application.Queries.Orders;

public class GetOrdersQuery : IRequest<IEnumerable<OrderResponse>>
{
}