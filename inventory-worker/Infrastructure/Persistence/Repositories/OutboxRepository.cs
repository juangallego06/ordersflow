using InventoryWorker.Application.Interfaces.Repositories;
using InventoryWorker.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryWorker.Infrastructure.Persistence.Repositories;

public class OutboxRepository(InventoryDbContext context) : IOutboxRepository
{
    private readonly InventoryDbContext _context = context;

    public async Task AddAsync(OutboxMessage message)
    {
        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync();
    }

    public async Task<List<OutboxMessage>> GetPendingAsync()
    {
        return await _context.OutboxMessages.Where(m => m.ProcessedOn == null).AsNoTracking().ToListAsync();
    }

    public async Task MarkAsPublishedAsync(Guid id)
    {
        var message = await _context.OutboxMessages.FindAsync(id);
        if (message != null)
        {
            message.MarkAsProcessed();
            await _context.SaveChangesAsync();
        }
    }
}

