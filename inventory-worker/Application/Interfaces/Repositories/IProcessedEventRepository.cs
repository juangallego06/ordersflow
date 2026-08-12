namespace InventoryWorker.Application.Interfaces.Repositories;

public interface IProcessedEventRepository
{
    Task<bool> TryMarkAsProcessedAsync(Guid eventId);
}
