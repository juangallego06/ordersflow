using OrdersApi.Application.Interfaces;

namespace OrdersApi.Infrastructure.Persistence;

public class UnitOfWork(OrdersDbContext context) : IUnitOfWork
{
    private readonly OrdersDbContext _context = context;

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
