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