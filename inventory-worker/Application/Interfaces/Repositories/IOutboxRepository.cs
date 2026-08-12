using InventoryWorker.Application.Models;

namespace InventoryWorker.Application.Interfaces.Repositories;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message);
    Task<List<OutboxMessage>> GetPendingAsync();
    Task MarkAsPublishedAsync(Guid id);
}
