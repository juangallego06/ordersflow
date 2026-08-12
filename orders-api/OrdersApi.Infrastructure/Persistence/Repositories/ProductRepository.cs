using Microsoft.EntityFrameworkCore;
using OrdersApi.Application.Interfaces.Repositories;
using OrdersApi.Domain.Entities;

namespace OrdersApi.Infrastructure.Persistence.Repositories;

public class ProductRepository(OrdersDbContext context) : IProductRepository
{
    private readonly OrdersDbContext _context = context;

    public async Task<Product?> GetBySkuAsync(string sku)
    {
        return await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Sku == sku);
    }
}
