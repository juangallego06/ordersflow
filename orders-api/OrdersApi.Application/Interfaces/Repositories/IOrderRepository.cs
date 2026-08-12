using OrdersApi.Domain.Entities;

namespace OrdersApi.Application.Interfaces.Repositories;

public interface IOrderRepository
{
    Task<IEnumerable<Order>> GetAllOrdersAsync();

    Task<Order?> GetOrderByIdAsync(string orderId);

    Task<Order> CreateOrderAsync(Order order);
}
