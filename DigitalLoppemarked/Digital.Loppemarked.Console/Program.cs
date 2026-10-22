using DigitalLoppemarkedLib.Data;
using DigitalLoppemarkedLib.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;

var password = Environment.GetEnvironmentVariable("LOPPEMARKED_DB_PASSWORD")
               ?? throw new InvalidOperationException("Set LOPPEMARKED_DB_PASSWORD first.");

var connectionString =
    $"Host=localhost;Port=5432;Database=postgres;Username=postgres;Password={password}";

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<OrderStatus>("order_status", new NpgsqlNullNameTranslator());
await using var dataSource = dataSourceBuilder.Build();

var options = new DbContextOptionsBuilder<LoppemarkedDbContext>()
    .UseNpgsql(dataSource)
    .Options;
//67
await using var db = new LoppemarkedDbContext(options);
var products = await db.Products.Where(p => p.IsActive).ToListAsync();
Console.WriteLine($"Loaded {products.Count} products");