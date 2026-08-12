namespace InventoryWorker.Domain.Entities;

public class Stock
{
    public string Sku { get; private set; }

    public int Available { get; private set; }

    private Stock() { }
}
