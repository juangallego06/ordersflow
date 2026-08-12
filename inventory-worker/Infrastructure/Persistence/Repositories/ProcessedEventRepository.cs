using InventoryWorker.Application.Interfaces.Repositories;
using InventoryWorker.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InventoryWorker.Infrastructure.Persistence.Repositories;

public class ProcessedEventRepository(InventoryDbContext context) : IProcessedEventRepository
{
    private readonly InventoryDbContext _context = context;

    public async Task<bool> TryMarkAsProcessedAsync(Guid eventId)
    {
        _context.ProcessedEvents.Add(new ProcessedEvent(eventId));

        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
        {
            return false;
        }
    }
}
