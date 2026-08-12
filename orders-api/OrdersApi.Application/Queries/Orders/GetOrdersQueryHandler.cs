using MediatR;
using OrdersApi.Application.DTOs;
using OrdersApi.Application.Interfaces.Repositories;
using OrdersApi.Application.Mappings;

namespace OrdersApi.Application.Queries.Orders;

public class GetOrdersQueryHandler(IOrderRepository orderRepository) : IRequestHandler<GetOrdersQuery, IEnumerable<OrderResponse>>
{
    private readonly IOrderRepository _orderRepository = orderRepository;

    public async Task<IEnumerable<OrderResponse>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.GetAllOrdersAsync();
        return orders.Select(order => order.ToResponse());
    }
}