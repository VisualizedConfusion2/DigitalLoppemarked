// Service/OrderService.cs
using DigitalLoppemarkedLib.Data.Repositories;
using DigitalLoppemarkedLib.Models;

namespace DigitalLoppemarkedLib.Service;

public class OrderService
{
    private readonly IOrderRepository _orders;
    public OrderService(IOrderRepository orders) => _orders = orders;

    public Task<Order> PlaceOrderAsync(Order order, IReadOnlyList<CartItem> cart, CancellationToken ct = default)
    {
        if (cart.Count == 0) throw new InvalidOperationException("Kurven er tom.");
        if (cart.Any(i => i.Quantity < 1)) throw new ArgumentException("Antal skal være mindst 1.");

        // Kun produkt-id og antal sendes videre. Pris og navn hentes i databasen.
        var lines = cart.Select(i => new OrderRequestLine(i.Product.Id, i.Quantity)).ToList();
        return _orders.PlaceOrderAsync(order, lines, ct);
    }

    public Task<Order?> GetOrderByIdAsync(int id, CancellationToken ct = default) =>
        _orders.GetByIdAsync(id, ct);
}