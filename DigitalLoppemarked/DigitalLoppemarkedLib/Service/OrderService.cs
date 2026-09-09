using DigitalLoppemarkedLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigitalLoppemarkedLib.Service
{
    // Services/OrderService.cs
    public class OrderService
    {
        private readonly List<Order> _orders = new();
        private readonly ProductService _productService;
        private int _nextId = 1;

        public OrderService(ProductService productService)
        {
            _productService = productService;
        }

        public async Task<Order> PlaceOrderAsync(Order order, IReadOnlyList<CartItem> cartItems)
        {
            order.Id = _nextId++;
            order.Lines = cartItems.Select(ci => new OrderLine
            {
                ProductId = ci.Product.Id,
                ProductName = ci.Product.Name,
                UnitPrice = ci.Product.Price,
                Quantity = ci.Quantity
            }).ToList();

            // reduce stock for each item; roll back conceptually isn't handled here
            // (with EF Core you'd wrap this in a transaction)
            foreach (var item in cartItems)
            {
                var ok = await _productService.ReduceStockAsync(item.Product.Id, item.Quantity);
                if (!ok)
                    throw new InvalidOperationException($"Not enough stock for {item.Product.Name}");
            }

            order.Status = OrderStatus.Paid; // stub — real payment goes here
            _orders.Add(order);
            return order;
        }

        public Task<List<Order>> GetOrdersAsync() => Task.FromResult(_orders.ToList());

        public Task<Order?> GetOrderByIdAsync(int id) =>
            Task.FromResult(_orders.FirstOrDefault(o => o.Id == id));
    }
}
