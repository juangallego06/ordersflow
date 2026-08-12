using InventoryWorker.Application.Models;
using InventoryWorker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryWorker.Infrastructure.Persistence;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
    : base(options)
    {
    }

    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Stock>(entity =>
        {
            entity.ToTable("Stock");
            entity.HasKey(s => s.Sku);
            entity.Property(s => s.Sku).HasMaxLength(50);
        });

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.ToTable("ProcessedEvents");
            entity.HasKey(p => p.EventId);
            entity.Property(p => p.ProcessedAt)
                .HasDefaultValueSql("SYSDATETIME()")
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.EventType).HasMaxLength(100);
        });
    }
}
