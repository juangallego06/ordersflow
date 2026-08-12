using OrdersApi.Application.Models;

namespace OrdersApi.Application.Interfaces.Repositories;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message);

    Task<IEnumerable<OutboxMessage>> GetPendingMessagesAsync();

    Task MarkAsProcessedAsync(Guid messageId);
}
