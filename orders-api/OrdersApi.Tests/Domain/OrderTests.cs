using OrdersApi.Domain.Entities;
using OrdersApi.Domain.Enums;
using Xunit;

namespace OrdersApi.Tests.Domain;

public class OrderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_ConCantidadFueraDeRango_LanzaArgumentException(int quantity)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Order.Create("Juan", "ABC-01", quantity));

        Assert.Equal("quantity", exception.ParamName);
    }

    [Fact]
    public void Create_ConDatosValidos_QuedaEnPending()
    {
        var order = Order.Create("Juan", "ABC-01", 5);

        Assert.Equal(OrderStatus.Pending, order.OrderStatus);
        Assert.Equal("Juan", order.CustomerName);
        Assert.Equal("ABC-01", order.Sku);
        Assert.Equal(5, order.Quantity);
    }

    [Fact]
    public void Confirm_DesdePending_CambiaAConfirmed()
    {
        var order = Order.Create("Juan", "ABC-01", 5);

        order.Confirm();

        Assert.Equal(OrderStatus.Confirmed, order.OrderStatus);
    }

    [Fact]
    public void Confirm_SobreUnPedidoYaConfirmado_LanzaInvalidOperationException()
    {
        var order = Order.Create("Juan", "ABC-01", 5);
        order.Confirm();

        Assert.Throws<InvalidOperationException>(() => order.Confirm());
    }
}