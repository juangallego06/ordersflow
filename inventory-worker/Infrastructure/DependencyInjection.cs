using InventoryWorker.Application;
using InventoryWorker.Application.Interfaces.Repositories;
using InventoryWorker.Infrastructure.Messaging;
using InventoryWorker.Infrastructure.Persistence;
using InventoryWorker.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace InventoryWorker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InventoryDb")
            ?? throw new InvalidOperationException("La cadena de conexión 'InventoryDb' no está configurada.");

        services.AddDbContext<InventoryDbContext>(options =>
            options.UseSqlServer(connectionString));


        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IProcessedEventRepository, ProcessedEventRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IConnection>(sp =>
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:Host"] ?? throw new InvalidOperationException("RabbitMQ:Host no configurado."),
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? throw new InvalidOperationException("RabbitMQ:Port no configurado.")),
                UserName = configuration["RabbitMQ:User"] ?? throw new InvalidOperationException("RabbitMQ:User no configurado."),
                Password = configuration["RabbitMQ:Password"] ?? throw new InvalidOperationException("RabbitMQ:Password no configurado.")
            };

            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        services.AddHostedService<OutboxPublisherBackgroundService>();
        services.AddHostedService<OrderCreatedConsumerBackgroundService>();

        return services;
    }
}