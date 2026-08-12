using Microsoft.EntityFrameworkCore;
using OrdersApi.Application.Interfaces.Repositories;
using OrdersApi.Domain.Entities;

namespace OrdersApi.Infrastructure.Persistence.Repositories;

public class OrderRepository(OrdersDbContext context) : IOrderRepository
{
    private readonly OrdersDbContext _context = context;

    public async Task<Order> CreateOrderAsync(Order order)
    {
        await _context.Orders.AddAsync(order);
        return order;
    }

    public async Task<IEnumerable<Order>> GetAllOrdersAsync()
    {
        return await _context.Orders.AsNoTracking().ToListAsync();
    }

    public async Task<Order?> GetOrderByIdAsync(string orderId)
    {
        return await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
    }
}
