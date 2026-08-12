namespace InventoryWorker.Application.Interfaces.Repositories;

public interface IStockRepository
{
    Task<bool> TryReserveAsync(string sku, int cantidad);
}
