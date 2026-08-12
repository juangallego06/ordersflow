using Microsoft.EntityFrameworkCore;
using OrdersApi.Application.Interfaces.Repositories;
using OrdersApi.Application.Models;

namespace OrdersApi.Infrastructure.Persistence.Repositories;

public class OutboxRepository(OrdersDbContext context) : IOutboxRepository
{
    private readonly OrdersDbContext _context = context;

    public async Task AddAsync(OutboxMessage message)
    {
        await _context.OutboxMessages.AddAsync(message);
    }

    public async Task<IEnumerable<OutboxMessage>> GetPendingMessagesAsync()
    {
        return await _context.OutboxMessages.Where(m => m.ProcessedOn == null).AsNoTracking().ToListAsync();
    }

    public async Task MarkAsProcessedAsync(Guid messageId)
    {
        var message = await _context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == messageId);

        if (message != null)
        {
            message.MarkAsProcessed();
        }
    }
}
