using System;
using System.Collections.Generic;
using DigitalLoppemarkedLib.Data;
using DigitalLoppemarkedLib.Data.Repositories;
using DigitalLoppemarkedLib.Models;
using DigitalLoppemarkedLib.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var connString = config.GetConnectionString("Loppemarked")
    ?? throw new InvalidOperationException(
        "Connection string 'Loppemarked' mangler. Kør dotnet user-secrets set.");

await using var dataSource = new NpgsqlDataSourceBuilder(connString).Build();

var productService = new ProductService(new ProductRepository(dataSource));
var orderService = new OrderService(
    new OrderRepository(dataSource, NullLogger<OrderRepository>.Instance));
var cart = new CartService();

bool running = true;
while (running)
{
    Console.WriteLine();
    Console.WriteLine("1) Vis produkter   2) Søg   3) Læg i kurv   4) Vis kurv   5) Bestil   0) Afslut");
    Console.Write("Valg: ");

    switch (Console.ReadLine())
    {
        case "1":
            {
                var page = await productService.GetProductsAsync(1, 50, ProductSort.NameAsc);
                Show(page.Items);
                break;
            }

        case "2":
            {
                var term = Ask("Søgeord");
                var page = await productService.SearchAsync(term, 1, 50, ProductSort.NameAsc);
                if (page.Items.Count == 0) Console.WriteLine("Ingen produkter fundet.");
                else Show(page.Items);
                break;
            }

        case "3":
            {
                if (!int.TryParse(Ask("Produkt-id"), out var id))
                {
                    Console.WriteLine("Ugyldigt id.");
                    break;
                }
                if (!int.TryParse(Ask("Antal"), out var qty) || qty < 1)
                {
                    Console.WriteLine("Antal skal være et tal på mindst 1.");
                    break;
                }

                var product = await productService.GetProductByIdAsync(id);
                if (product is null)
                    Console.WriteLine("Produktet findes ikke.");
                else
                {
                    cart.AddToCart(product, qty);
                    Console.WriteLine($"{product.Name} lagt i kurven.");
                }
                break;
            }

        case "4":
            {
                if (cart.Items.Count == 0) Console.WriteLine("Kurven er tom.");
                foreach (var item in cart.Items)
                    Console.WriteLine($"{item.Product.Name} x{item.Quantity} = {item.Subtotal:0.00} kr");
                Console.WriteLine($"Total: {cart.Total:0.00} kr");
                break;
            }

        case "5":
            {
                if (cart.Items.Count == 0)
                {
                    Console.WriteLine("Kurven er tom.");
                    break;
                }

                var order = new Order
                {
                    CustomerName = Ask("Navn"),
                    Email = Ask("Email"),
                    ShippingAddress = Ask("Adresse"),
                    City = Ask("By"),
                    PostalCode = Ask("Postnummer"),
                    Country = Ask("Land"),
                };

                try
                {
                    var placed = await orderService.PlaceOrderAsync(order, cart.Items);
                    cart.Clear();
                    Console.WriteLine($"Ordre {placed.Id} er oprettet. Total: {placed.Total:0.00} kr");
                }
                catch (InsufficientStockException ex)
                {
                    Console.WriteLine($"Bestillingen blev annulleret: {ex.Message}");
                }
                catch (DataAccessException ex)
                {
                    Console.WriteLine($"Bestillingen blev annulleret: {ex.Message}");
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    Console.WriteLine($"Bestillingen blev annulleret: {ex.Message}");
                }
                break;
            }

        case "0":
            running = false;
            break;

        default:
            Console.WriteLine("Ukendt valg.");
            break;
    }
}

static string Ask(string prompt)
{
    Console.Write($"{prompt}: ");
    return Console.ReadLine()?.Trim() ?? "";
}

static void Show(IEnumerable<Product> products)
{
    foreach (var p in products)
        Console.WriteLine($"{p.Id,3} | {p.Name,-28} | {p.Price,8:0.00} kr | lager: {p.StockQuantity}");
}