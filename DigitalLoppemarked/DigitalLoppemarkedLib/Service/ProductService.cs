using DigitalLoppemarkedLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigitalLoppemarkedLib.Service
{
    // Services/ProductService.cs
    public class ProductService
    {
        private readonly List<Product> _products = new()
    {
        new Product { Id = 1, Name = "Wireless Mouse", Description = "Ergonomic wireless mouse", Price = 24.99m, ImageUrl = "images/mouse.jpg", StockQuantity = 50 },
        new Product { Id = 2, Name = "Mechanical Keyboard", Description = "RGB mechanical keyboard", Price = 79.99m, ImageUrl = "images/keyboard.jpg", StockQuantity = 30 },
        new Product { Id = 3, Name = "USB-C Hub", Description = "7-in-1 USB-C hub", Price = 34.99m, ImageUrl = "images/hub.jpg", StockQuantity = 100 },
    };

        public Task<List<Product>> GetProductsAsync()
        {
            return Task.FromResult(_products.ToList());
        }

        public Task<Product?> GetProductByIdAsync(int id)
        {
            var product = _products.FirstOrDefault(p => p.Id == id);
            return Task.FromResult(product);
        }

        public Task<List<Product>> SearchAsync(string term)
        {
            var results = _products
                .Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return Task.FromResult(results);
        }

        public Task<bool> ReduceStockAsync(int productId, int quantity)
        {
            var product = _products.FirstOrDefault(p => p.Id == productId);
            if (product is null || product.StockQuantity < quantity)
                return Task.FromResult(false);

            product.StockQuantity -= quantity;
            return Task.FromResult(true);
        }
    }
}
