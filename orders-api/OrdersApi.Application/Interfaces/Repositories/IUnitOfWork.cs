namespace OrdersApi.Application.Interfaces.Repositories;

public interface IUnitOfWork
{
    Task SaveChangesAsync();
}
