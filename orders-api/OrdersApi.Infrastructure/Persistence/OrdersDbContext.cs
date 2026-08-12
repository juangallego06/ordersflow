using Microsoft.EntityFrameworkCore;
using OrdersApi.Application.Models;
using OrdersApi.Domain.Entities;

namespace OrdersApi.Infrastructure.Persistence;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).ValueGeneratedOnAdd();
            entity.Property(o => o.OrderId).IsRequired().HasMaxLength(50);
            entity.HasIndex(o => o.OrderId).IsUnique();
            entity.Property(o => o.CustomerName).IsRequired().HasMaxLength(150);
            entity.Property(o => o.Sku).IsRequired().HasMaxLength(50);
            entity.Property(o => o.Quantity).IsRequired();
            entity.Property(o => o.OrderStatus)
                  .IsRequired()
                  .HasMaxLength(20)
                  .HasConversion<string>(); // guarda el enum como 'Pending'/'Confirmed'/'Rejected', igual que el CHECK de init.sql
            entity.Property(o => o.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(p => p.Sku); // Product no tiene Id; su clave natural es el Sku
            entity.Property(p => p.Sku).HasMaxLength(50);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.EventType).IsRequired().HasMaxLength(100);
            entity.Property(m => m.Payload).IsRequired();
            entity.Property(m => m.OccurredOn).IsRequired();
            entity.Property(m => m.ProcessedOn);
        });
    }
}
