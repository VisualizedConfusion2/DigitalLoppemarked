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
        new Product { Id = 1, Name = "Wireless Mouse", Description = "Ergonomic wireless mouse", Price = 24.99m, ImageUrl = "images/mouse.svg", StockQuantity = 50 },
        new Product { Id = 2, Name = "Mechanical Keyboard", Description = "RGB mechanical keyboard", Price = 79.99m, ImageUrl = "images/keyboard.svg", StockQuantity = 30 },
        new Product { Id = 3, Name = "USB-C Hub", Description = "7-in-1 USB-C hub", Price = 34.99m, ImageUrl = "images/hub.svg", StockQuantity = 100 },
        new Product { Id = 4, Name = "Webcam 1080p", Description = "Full HD webcam with microphone", Price = 44.99m, ImageUrl = "images/webcam.svg", StockQuantity = 20 },
        new Product { Id = 5, Name = "Noise-Cancelling Headset", Description = "Over-ear headset with ANC", Price = 119.99m, ImageUrl = "images/headset.svg", StockQuantity = 15 },
        new Product { Id = 6, Name = "Laptop Stand", Description = "Adjustable aluminium laptop stand", Price = 29.99m, ImageUrl = "images/stand.svg", StockQuantity = 0 },
        new Product { Id = 7, Name = "27\" Monitor", Description = "QHD 144Hz IPS monitor", Price = 249.99m, ImageUrl = "images/monitor.svg", StockQuantity = 8 },
        new Product { Id = 8, Name = "Desk Mat", Description = "Large felt desk mat", Price = 19.99m, ImageUrl = "images/deskmat.svg", StockQuantity = 60 },
        // Inactive product: must never appear in listings/search and must 404 when
        // its ID is guessed directly (e.g. /product/9).
        new Product { Id = 9, Name = "Discontinued Gadget", Description = "No longer for sale", Price = 9.99m, ImageUrl = "images/placeholder.svg", StockQuantity = 5, IsActive = false },
    };

        // Defensive upper bound so a manipulated page size can't request huge pages.
        private const int MaxPageSize = 50;

        public Task<List<Product>> GetProductsAsync()
        {
            return Task.FromResult(_products.Where(p => p.IsActive).ToList());
        }

        public Task<PagedResult<Product>> GetProductsAsync(int page, int pageSize, ProductSort sort)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 6;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            var active = _products.Where(p => p.IsActive);

            IEnumerable<Product> query = sort switch
            {
                ProductSort.NameDesc => active.OrderByDescending(p => p.Name),
                ProductSort.PriceAsc => active.OrderBy(p => p.Price),
                ProductSort.PriceDesc => active.OrderByDescending(p => p.Price),
                _ => active.OrderBy(p => p.Name),
            };

            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<Product>
            {
                Items = items,
                TotalCount = _products.Count(p => p.IsActive),
                Page = page,
                PageSize = pageSize
            };

            return Task.FromResult(result);
        }

        public Task<Product?> GetProductByIdAsync(int id)
        {
            // Only return active products so inactive/hidden ones can't be reached by ID.
            var product = _products.FirstOrDefault(p => p.Id == id && p.IsActive);
            return Task.FromResult(product);
        }

        public Task<List<Product>> SearchAsync(string term)
        {
            var results = _products
                .Where(p => p.IsActive && p.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return Task.FromResult(results);
        }

        public Task<bool> ReduceStockAsync(int productId, int quantity)
        {
            var product = _products.FirstOrDefault(p => p.Id == productId && p.IsActive);
            if (product is null || quantity < 1 || product.StockQuantity < quantity)
                return Task.FromResult(false);

            product.StockQuantity -= quantity;
            return Task.FromResult(true);
        }
    }
}
