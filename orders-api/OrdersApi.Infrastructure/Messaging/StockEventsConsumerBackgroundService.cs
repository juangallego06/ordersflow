using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrdersApi.Application.Events;
using OrdersApi.Application.Interfaces;
using OrdersApi.Application.Interfaces.Repositories;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace OrdersApi.Infrastructure.Messaging;

public class StockEventsConsumerBackgroundService(IServiceScopeFactory scopeFactory, IConnection connection, ILogger<StockEventsConsumerBackgroundService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IConnection _connection = connection;
    private readonly ILogger<StockEventsConsumerBackgroundService> _logger = logger;
    private const string QueueName = "stock-events";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: QueueName, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            var eventType = ea.BasicProperties.Type;
            var payload = Encoding.UTF8.GetString(ea.Body.ToArray());

            try
            {
                await ProcessMessageAsync(eventType, payload);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (InvalidOperationException ex)
            {
                // El pedido ya no esta en Pending: evento duplicado o tardio. Descarte seguro.
                _logger.LogWarning(ex, "Evento {EventType} ignorado (pedido ya no estaba en Pending).", eventType);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando evento {EventType}, se reintentara.", eventType);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task ProcessMessageAsync(string? eventType, string payload)
    {
        using var scope = _scopeFactory.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var stockEvent = JsonSerializer.Deserialize<StockEvent>(payload, JsonOptions)
            ?? throw new InvalidOperationException("Payload de evento invalido.");

        var order = await orderRepository.GetOrderByIdAsync(stockEvent.OrderId);
        if (order is null)
        {
            _logger.LogWarning("Pedido {OrderId} no encontrado para el evento {EventType}.", stockEvent.OrderId, eventType);
            return;
        }

        switch (eventType)
        {
            case "StockReserved":
                order.Confirm();
                break;
            case "StockRejected":
                order.Reject();
                break;
            default:
                throw new InvalidOperationException($"Tipo de evento desconocido: {eventType}");
        }

        await unitOfWork.SaveChangesAsync();
    }

}
