namespace OrdersApi.Application.DTOs;

public class OrderResponse
{
    public required string Id { get; init; }
    public required string CustomerName { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
}
