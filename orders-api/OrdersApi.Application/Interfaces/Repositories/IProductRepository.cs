using OrdersApi.Domain.Entities;

namespace OrdersApi.Application.Interfaces.Repositories;

public interface IProductRepository
{
    Task<Product?> GetBySkuAsync(string sku);
}
