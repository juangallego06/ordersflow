namespace OrdersApi.Domain.Entities;

public class Product
{
    public string Sku { get; private set; }

    private Product() { }

    private Product(string sku)
    {
        Sku = sku;
    }

    public static Product Create(string sku)
    {
        if (string.IsNullOrEmpty(sku))
            throw new ArgumentException("El SKU no puede estar vacío.", nameof(sku));

        return new Product(sku);
    }
}
