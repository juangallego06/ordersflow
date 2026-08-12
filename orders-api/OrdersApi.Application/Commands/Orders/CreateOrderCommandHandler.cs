using MediatR;
using OrdersApi.Application.DTOs;
using OrdersApi.Application.Events;
using OrdersApi.Application.Interfaces.Repositories;
using OrdersApi.Application.Mappings;
using OrdersApi.Application.Models;
using OrdersApi.Domain.Entities;
using System.Text.Json;

namespace OrdersApi.Application.Commands.Orders;

public class CreateOrderCommandHandler(
    IProductRepository productRepository,
    IOrderRepository orderRepository,
    IOutboxRepository outboxRepository,
    IUnitOfWork unitOfWork
    ) : IRequestHandler<CreateOrderCommand, OrderResponse>
{
    private readonly IProductRepository _productRepository = productRepository;
    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly IOutboxRepository _outboxRepository = outboxRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<OrderResponse> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        //1. Validamos si el SKU existe
        var product = await _productRepository.GetBySkuAsync(request.Sku);
        if (product is null)
            throw new ArgumentException($"El SKU '{request.Sku}' no existe en el catálogo.", nameof(request.Sku));

        //2. Creamos el objeto de la orden mediante el metodo Create que contiene reglas del negocio. Si hay un excepción lo captura el middleware
        var order = Order.Create(request.CustomerName, request.Sku, request.Quantity);

        //3. Persistimos en la BD
        await _orderRepository.CreateOrderAsync(order);

        //4. Creamos el objeto del evento
        var orderCreatedEvent = new OrderCreatedEvent(
            EventId: Guid.NewGuid(),
            OrderId: order.OrderId,
            Sku: order.Sku,
            Cantidad: order.Quantity,
            OcurridoEn: DateTime.UtcNow
        );

        //5. Creamos el payload
        var payload = JsonSerializer.Serialize( orderCreatedEvent, SerializerOptions );

        //6. Creamos el mensaje del Outbox
        var outboxMessage = OutboxMessage.Create("OrderCreated", payload);

        //7. Persistimos el mensaje en la BD
        await _outboxRepository.AddAsync(outboxMessage);

        //8. Confirmamos con la unidad de trabajo unica la atomicidad en ambas persistencias
        await _unitOfWork.SaveChangesAsync();

        //9. Retornamos
        return order.ToResponse();
    }
}
