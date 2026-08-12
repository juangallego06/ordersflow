using InventoryWorker.Application.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InventoryWorker.Infrastructure.Persistence.Repositories;

public class StockRepository(InventoryDbContext context) : IStockRepository
{
    private readonly InventoryDbContext _context = context;

    public async Task<bool> TryReserveAsync(string sku, int cantidad)
    {
        var affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Stock SET Available = Available - {cantidad} WHERE Sku = {sku} AND Available >= {cantidad}");

        return affectedRows == 1;
    }
}
