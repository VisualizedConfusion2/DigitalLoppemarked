// Data/Repositories/Interfaces.cs
using DigitalLoppemarkedLib.Models;

namespace DigitalLoppemarkedLib.Data.Repositories;

/// <summary>
/// Handles data access for Products.
/// Uses paged results to avoid loading the entire database into memory.
/// </summary>
public interface IProductRepository
{
    Task<PagedResult<Product>> GetActivePageAsync(int page, int pageSize, ProductSort sort, CancellationToken ct);
    Task<Product?> GetActiveByIdAsync(int id, CancellationToken ct);
    Task<PagedResult<Product>> SearchActiveAsync(string term, int page, int pageSize, ProductSort sort, CancellationToken ct);
}

/// <summary>
/// Represents a single item within an order request.
/// </summary>
public readonly record struct OrderRequestLine(int ProductId, int Quantity);

/// <summary>
/// Handles order placement and retrieval.
/// </summary>
public interface IOrderRepository
{
    Task<Order> PlaceOrderAsync(Order order, IReadOnlyList<OrderRequestLine> lines, CancellationToken ct);
    Task<Order?> GetByIdAsync(int id, CancellationToken ct);
}

/// <summary>
/// Handles user account data and authentication.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByIdAsync(int id, CancellationToken ct);
    Task<int> CreateUserAsync(User user, CancellationToken ct);
    Task UpdateUserAsync(User user, CancellationToken ct);
}