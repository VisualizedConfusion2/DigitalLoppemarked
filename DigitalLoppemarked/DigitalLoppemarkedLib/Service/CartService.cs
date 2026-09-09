using DigitalLoppemarkedLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigitalLoppemarkedLib.Service
{
    public class CartService
    {
        public event Action? OnChange;
        private List<CartItem> _items = new();

        public IReadOnlyList<CartItem> Items => _items;
        public decimal Total => _items.Sum(i => i.Subtotal);

        public void AddToCart(Product product, int qty = 1)
        {
            var existing = _items.FirstOrDefault(i => i.Product.Id == product.Id);
            if (existing != null)
                existing.Quantity += qty;
            else
                _items.Add(new CartItem { Product = product, Quantity = qty });

            NotifyStateChanged();
        }

        public void RemoveFromCart(int productId)
        {
            _items.RemoveAll(i => i.Product.Id == productId);
            NotifyStateChanged();
        }

        public void Clear()
        {
            _items.Clear();
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
