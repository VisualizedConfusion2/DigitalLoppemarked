// Data/Repositories/Interfaces.cs
using DigitalLoppemarkedLib.Models;

namespace DigitalLoppemarkedLib.Data.Repositories;

public interface IProductRepository
{
    Task<PagedResult<Product>> GetActivePageAsync(int page, int pageSize, ProductSort sort, CancellationToken ct);
    Task<Product?> GetActiveByIdAsync(int id, CancellationToken ct);
    Task<PagedResult<Product>> SearchActiveAsync(string term, int page, int pageSize, ProductSort sort, CancellationToken ct);
}

public readonly record struct OrderRequestLine(int ProductId, int Quantity);

public interface IOrderRepository
{
    Task<Order> PlaceOrderAsync(Order order, IReadOnlyList<OrderRequestLine> lines, CancellationToken ct);
    Task<Order?> GetByIdAsync(int id, CancellationToken ct);
}

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByIdAsync(int id, CancellationToken ct);
    Task<int> CreateUserAsync(User user, CancellationToken ct);
    Task UpdateUserAsync(User user, CancellationToken ct);
    Task CreateVerificationTokenAsync(int userId, string token, DateTime expiresAt, CancellationToken ct);
    Task<int?> GetUserIdByTokenAsync(string token, CancellationToken ct);
    Task DeleteVerificationTokenAsync(int userId, CancellationToken ct);
}