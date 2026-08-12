namespace OrdersApi.Application.Interfaces;

public interface IUnitOfWork
{
    Task SaveChangesAsync();
}
