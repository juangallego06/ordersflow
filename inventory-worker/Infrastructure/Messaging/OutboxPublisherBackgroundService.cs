using InventoryWorker.Application.Interfaces.Repositories;
using RabbitMQ.Client;
using System.Text;

namespace InventoryWorker.Infrastructure.Messaging;

public class OutboxPublisherBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConnection connection,
    ILogger<OutboxPublisherBackgroundService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IConnection _connection = connection;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger = logger;
    private const string QueueName = "stock-events";
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publicando mensajes pendientes del Outbox");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task PublishPendingMessagesAsync(CancellationToken stoppingToken)
    {
        // Scope manual: IOutboxRepository es scoped, este servicio es singleton.
        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var pendingMessages = await outboxRepository.GetPendingAsync();

        using var channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: QueueName, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: stoppingToken);

        foreach (var message in pendingMessages)
        {
            var body = Encoding.UTF8.GetBytes(message.Payload);
            var properties = new BasicProperties { Persistent = true, Type = message.EventType };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: QueueName,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: stoppingToken);

            await outboxRepository.MarkAsPublishedAsync(message.Id);
        }
    }
}