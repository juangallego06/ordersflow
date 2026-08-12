using OrdersApi.Domain.Enums;

namespace OrdersApi.Domain.Entities;

public class Order
{
    public int Id { get; private set; }
    public string OrderId { get; private set; }
    public string CustomerName { get; private set; }
    public string Sku { get; private set; }
    public int Quantity { get; private set; }
    public OrderStatus OrderStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Order() { }

    private Order(string orderId, string customerName, string sku, int quantity)
    {
        OrderId = orderId;
        CustomerName = customerName;
        Sku = sku;
        Quantity = quantity;
        OrderStatus = OrderStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public static Order Create(string customerName, string sku, int quantity)
    {
        if (string.IsNullOrEmpty(customerName))
            throw new ArgumentException("El nombre del cliente no puede estar vacío.", nameof(customerName));
        
        if (string.IsNullOrEmpty(sku))
            throw new ArgumentException("El SKU no puede estar vacío.", nameof(sku));

        if (quantity < 1 || quantity > 100)
            throw new ArgumentException("La cantidad debe estar entre 1 y 100.", nameof(quantity));

        var orderId = Guid.NewGuid().ToString();
        return new Order(orderId, customerName, sku, quantity);
    }

    public void Confirm()
    {
        if (OrderStatus != OrderStatus.Pending)
            throw new InvalidOperationException("Solo se pueden confirmar pedidos pendientes.");

        OrderStatus = OrderStatus.Confirmed;
    }

    public void Reject()
    {
        if (OrderStatus != OrderStatus.Pending)
            throw new InvalidOperationException("Solo se pueden rechazar pedidos pendientes.");

        OrderStatus = OrderStatus.Rejected;
    }
}
