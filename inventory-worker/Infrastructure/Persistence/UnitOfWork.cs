using InventoryWorker.Application;
using InventoryWorker.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace InventoryWorker.Infrastructure.Persistence;

public class UnitOfWork(InventoryDbContext context) : IUnitOfWork
{
    private readonly InventoryDbContext _context = context;
    private IDbContextTransaction? _transaction;

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitAsync()
    {
        if (_transaction is null) return;

        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync()
    {
        if (_transaction is null) return;

        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}