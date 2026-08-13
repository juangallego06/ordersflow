using Moq;
using OrdersApi.Application.Commands.Orders;
using OrdersApi.Application.Interfaces;
using OrdersApi.Application.Interfaces.Repositories;
using OrdersApi.Application.Models;
using OrdersApi.Domain.Entities;

namespace OrdersApi.Tests.Application;

public class CreateOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_ConSkuInexistente_LanzaArgumentException_YNoPersisteNada()
    {
        // Arrange
        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(r => r.GetBySkuAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var orderRepository = new Mock<IOrderRepository>();
        var outboxRepository = new Mock<IOutboxRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new CreateOrderCommandHandler(
            productRepository.Object,
            orderRepository.Object,
            outboxRepository.Object,
            unitOfWork.Object);

        var command = new CreateOrderCommand
        {
            CustomerName = "Juan",
            Sku = "ABC-INEXISTENTE",
            Quantity = 5
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.Handle(command, CancellationToken.None));

        orderRepository.Verify(r => r.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
        outboxRepository.Verify(r => r.AddAsync(It.IsAny<OutboxMessage>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}