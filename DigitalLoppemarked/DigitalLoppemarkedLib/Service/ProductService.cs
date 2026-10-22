using DigitalLoppemarkedLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        // Story 7 (Søgning): the search term is validated and length-limited so it can't
        // be abused for injection or DoS. Allow only Unicode letters (incl. æøå), digits,
        // whitespace and hyphen — this rejects SQLi/XSS payloads such as ' OR 1=1 --,
        // '; DROP TABLE ... and <script>...</script> outright.
        public const int MaxSearchLength = 100;

        private static readonly Regex AllowedSearch =
            new(@"^[\p{L}\p{N}\s\-]*$", RegexOptions.Compiled);

        public Task<List<Product>> GetProductsAsync()
        {
            return Task.FromResult(_products.Where(p => p.IsActive).ToList());
        }

        public Task<PagedResult<Product>> GetProductsAsync(int page, int pageSize, ProductSort sort)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 6;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            var query = ApplySort(_products.Where(p => p.IsActive), sort);

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
            return Task.FromResult(SearchCore(term).ToList());
        }

        // Paged search used by the UI. Reuses the same page/pageSize clamping and sort
        // options as the browse listing so a manipulated query can't request huge pages.
        public Task<PagedResult<Product>> SearchAsync(string term, int page, int pageSize, ProductSort sort)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 6;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            var matches = SearchCore(term).ToList();
            var items = ApplySort(matches, sort)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<Product>
            {
                Items = items,
                TotalCount = matches.Count,
                Page = page,
                PageSize = pageSize
            };

            return Task.FromResult(result);
        }

        // Validates the term and returns matching ACTIVE products (name or description).
        // Storage is an in-memory list queried with LINQ, which is injection-safe by
        // construction; if/when the ORM-less data layer (story 14) lands, these lookups
        // must use parameterized commands. An invalid term (empty, too long, or
        // containing disallowed characters) yields no results rather than throwing, so
        // SQLi/XSS payloads are neutralised and inactive products stay hidden.
        private IEnumerable<Product> SearchCore(string term)
        {
            term = (term ?? "").Trim();
            if (term.Length == 0 || term.Length > MaxSearchLength || !AllowedSearch.IsMatch(term))
                return Enumerable.Empty<Product>();

            return _products.Where(p => p.IsActive &&
                (p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 p.Description.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        private static IEnumerable<Product> ApplySort(IEnumerable<Product> source, ProductSort sort) =>
            sort switch
            {
                ProductSort.NameDesc => source.OrderByDescending(p => p.Name),
                ProductSort.PriceAsc => source.OrderBy(p => p.Price),
                ProductSort.PriceDesc => source.OrderByDescending(p => p.Price),
                _ => source.OrderBy(p => p.Name),
            };

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
