using InventoryWorker.Application;
using InventoryWorker.Application.Events;
using InventoryWorker.Application.Interfaces;
using InventoryWorker.Application.Interfaces.Repositories;
using InventoryWorker.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace InventoryWorker.Infrastructure.Messaging;

public class OrderCreatedConsumerBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConnection connection,
    ILogger<OrderCreatedConsumerBackgroundService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IConnection _connection = connection;
    private readonly ILogger<OrderCreatedConsumerBackgroundService> _logger = logger;
    private const string QueueName = "order-created";

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
            var payload = Encoding.UTF8.GetString(ea.Body.ToArray());

            try
            {
                await ProcessMessageAsync(payload);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando order-created, se reintentara.");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task ProcessMessageAsync(string payload)
    {
        using var scope = _scopeFactory.CreateScope();
        var processedEventRepository = scope.ServiceProvider.GetRequiredService<IProcessedEventRepository>();
        var stockRepository = scope.ServiceProvider.GetRequiredService<IStockRepository>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var orderCreated = JsonSerializer.Deserialize<OrderCreatedEvent>(payload, JsonOptions)
            ?? throw new InvalidOperationException("Payload de evento invalido.");

        await unitOfWork.BeginTransactionAsync();

        try
        {
            var isNewEvent = await processedEventRepository.TryMarkAsProcessedAsync(orderCreated.EventId);
            if (!isNewEvent)
            {
                _logger.LogWarning("Evento {EventId} ya habia sido procesado, se descarta.", orderCreated.EventId);
                await unitOfWork.RollbackAsync();
                return;
            }

            var reserved = await stockRepository.TryReserveAsync(orderCreated.Sku, orderCreated.Cantidad);

            var outboxMessage = OutboxMessage.Create(
                eventType: reserved ? "StockReserved" : "StockRejected",
                payload: JsonSerializer.Serialize(new
                {
                    eventId = Guid.NewGuid(),
                    orderId = orderCreated.OrderId,
                    sku = orderCreated.Sku,
                    cantidad = orderCreated.Cantidad,
                    ocurridoEn = orderCreated.OcurridoEn
                }, JsonOptions));

            await outboxRepository.AddAsync(outboxMessage);

            await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
    }
}