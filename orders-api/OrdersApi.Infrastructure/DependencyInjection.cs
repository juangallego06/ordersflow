using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrdersApi.Application.Interfaces;
using OrdersApi.Application.Interfaces.Repositories;
using OrdersApi.Infrastructure.Messaging;
using OrdersApi.Infrastructure.Persistence;
using OrdersApi.Infrastructure.Persistence.Repositories;
using RabbitMQ.Client;

namespace OrdersApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrdersDb")
            ?? throw new InvalidOperationException("La cadena de conexión 'OrdersDb' no está configurada.");

        services.AddDbContext<OrdersDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
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
        services.AddHostedService<StockEventsConsumerBackgroundService>();

        return services;
    }
}
