// Data/Repositories/OrderRepository.cs
using System.Data;
using DigitalLoppemarkedLib.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DigitalLoppemarkedLib.Data.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly NpgsqlDataSource _db;
    private readonly ILogger<OrderRepository> _log;

    public OrderRepository(NpgsqlDataSource db, ILogger<OrderRepository> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<Order> PlaceOrderAsync(Order order, IReadOnlyList<OrderRequestLine> lines, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            order.Status = OrderStatus.Paid; // stub: rigtig betaling kommer her, FØR commit
            order.Id = await InsertOrderHeaderAsync(conn, tx, order, ct);
            order.Lines = new List<OrderLine>();

            foreach (var line in lines)
            {
                // Lager reduceres og pris/navn hentes i ÉN atomisk statement.
                // Hvis lageret er for lavt, returneres ingen række -> exception -> rollback.
                var (name, price) = await DecrementStockAsync(conn, tx, line, ct);

                var orderLine = new OrderLine
                {
                    ProductId = line.ProductId,
                    ProductName = name,
                    UnitPrice = price,
                    Quantity = line.Quantity,
                };

                await InsertOrderLineAsync(conn, tx, order.Id, orderLine, ct);
                order.Lines.Add(orderLine);
            }

            await tx.CommitAsync(ct);
            return order;
        }
        catch (Exception ex)
        {
            // Eksplicit rollback, så det er tydeligt i koden at alt fejler samlet.
            await tx.RollbackAsync(CancellationToken.None);

            if (ex is PostgresException pg)
            {
                // Detaljer logges internt; brugeren får en generisk besked (story 13).
                _log.LogError(pg, "DB-fejl ved oprettelse af ordre. SqlState={SqlState}", pg.SqlState);
                throw new DataAccessException("Ordren kunne ikke gemmes.", pg);
            }

            throw; // InsufficientStockException m.fl. sendes videre uændret
        }
    }

    public async Task<Order?> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        Order? order = null;

        await using (var cmd = new NpgsqlCommand(@"
            SELECT id, customer_name, email, shipping_address, city, postal_code,
                   country, order_date, status::text
            FROM orders WHERE id = @id", conn))
        {
            cmd.Parameters.AddWithValue("@id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                order = new Order
                {
                    Id = r.GetInt32(0),
                    CustomerName = r.GetString(1),
                    Email = r.GetString(2),
                    ShippingAddress = r.GetString(3),
                    City = r.GetString(4),
                    PostalCode = r.GetString(5),
                    Country = r.GetString(6),
                    OrderDate = r.GetDateTime(7),
                    Status = Enum.Parse<OrderStatus>(r.GetString(8)),
                };
            }
        }

        if (order is null) return null;

        await using (var cmd = new NpgsqlCommand(@"
            SELECT product_id, product_name, unit_price, quantity
            FROM order_lines WHERE order_id = @id ORDER BY id", conn))
        {
            cmd.Parameters.AddWithValue("@id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                order.Lines.Add(new OrderLine
                {
                    ProductId = r.GetInt32(0),
                    ProductName = r.GetString(1),
                    UnitPrice = r.GetDecimal(2),
                    Quantity = r.GetInt32(3),
                });
            }
        }

        return order;
    }

    // ---------- private helpers: alle SQL med parametre ----------

    private static async Task<int> InsertOrderHeaderAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Order o, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO orders (customer_name, email, shipping_address, city, postal_code,
                                country, order_date, status)
            VALUES (@customer_name, @email, @shipping_address, @city, @postal_code,
                    @country, @order_date, @status::order_status)
            RETURNING id";

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@customer_name", o.CustomerName);
        cmd.Parameters.AddWithValue("@email", o.Email);
        cmd.Parameters.AddWithValue("@shipping_address", o.ShippingAddress);
        cmd.Parameters.AddWithValue("@city", o.City);
        cmd.Parameters.AddWithValue("@postal_code", o.PostalCode);
        cmd.Parameters.AddWithValue("@country", o.Country);
        cmd.Parameters.AddWithValue("@order_date", o.OrderDate);
        cmd.Parameters.AddWithValue("@status", o.Status.ToString());

        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static async Task<(string Name, decimal Price)> DecrementStockAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, OrderRequestLine line, CancellationToken ct)
    {
        const string sql = @"
            UPDATE products
            SET    stock_quantity = stock_quantity - @qty
            WHERE  id = @id
              AND  is_active
              AND  stock_quantity >= @qty
            RETURNING name, price";

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@qty", line.Quantity);
        cmd.Parameters.AddWithValue("@id", line.ProductId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InsufficientStockException(line.ProductId);

        return (reader.GetString(0), reader.GetDecimal(1));
    }

    private static async Task InsertOrderLineAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int orderId, OrderLine l, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO order_lines (order_id, product_id, product_name, unit_price, quantity)
            VALUES (@order_id, @product_id, @product_name, @unit_price, @quantity)";

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@order_id", orderId);
        cmd.Parameters.AddWithValue("@product_id", l.ProductId);
        cmd.Parameters.AddWithValue("@product_name", l.ProductName);
        cmd.Parameters.AddWithValue("@unit_price", l.UnitPrice);
        cmd.Parameters.AddWithValue("@quantity", l.Quantity);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}