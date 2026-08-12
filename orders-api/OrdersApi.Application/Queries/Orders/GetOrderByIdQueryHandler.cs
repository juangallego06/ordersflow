using MediatR;
using OrdersApi.Application.DTOs;
using OrdersApi.Application.Interfaces.Repositories;
using OrdersApi.Application.Mappings;

namespace OrdersApi.Application.Queries.Orders;

public class GetOrderByIdQueryHandler(IOrderRepository orderRepository) : IRequestHandler<GetOrderByIdQuery, OrderResponse?>
{
    private readonly IOrderRepository _orderRepository = orderRepository;

    public async Task<OrderResponse?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetOrderByIdAsync(request.OrderId);
        return order?.ToResponse();
    }
}