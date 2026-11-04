// Data/Repositories/ProductRepository.cs
using DigitalLoppemarkedLib.Models;
using Npgsql;

namespace DigitalLoppemarkedLib.Data.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly NpgsqlDataSource _db;
    public ProductRepository(NpgsqlDataSource db) => _db = db;

    // Kolonnerækkefølgen her SKAL matche ReadProductsAsync.
    private const string Columns = "id, name, description, price, image_url, stock_quantity, is_active";

    // Whitelist (story: "Hvis dynamisk sortering bruges, whitelistes kolonnenavne").
    // Brugerinput går aldrig direkte ind i SQL; kun disse konstanter gør.
    private static readonly Dictionary<ProductSort, string> OrderByWhitelist = new()
    {
        [ProductSort.NameAsc] = "name ASC, id ASC",
        [ProductSort.NameDesc] = "name DESC, id ASC",
        [ProductSort.PriceAsc] = "price ASC, id ASC",
        [ProductSort.PriceDesc] = "price DESC, id ASC",
    };

    public async Task<PagedResult<Product>> GetActivePageAsync(int page, int pageSize, ProductSort sort, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        // Sorteringsfragmentet er hentet fra whitelisten, så konkatenation er sikker her.
        var sql = "SELECT " + Columns + " FROM products WHERE is_active " +
                  "ORDER BY " + OrderByWhitelist[sort] + " LIMIT @limit OFFSET @offset"; // nosemgrep: whitelisted fragment

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@limit", pageSize);
        cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
        var items = await ReadProductsAsync(cmd, ct);

        const string countSql = "SELECT COUNT(*) FROM products WHERE is_active";
        await using var countCmd = new NpgsqlCommand(countSql, conn);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        return new PagedResult<Product> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<Product?> GetActiveByIdAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT " + Columns + " FROM products WHERE id = @id AND is_active", conn);
        cmd.Parameters.AddWithValue("@id", id);
        return (await ReadProductsAsync(cmd, ct)).FirstOrDefault();
    }

    public async Task<PagedResult<Product>> SearchActiveAsync(string term, int page, int pageSize, ProductSort sort, CancellationToken ct)
    {
        // Wildcards i brugerinput escapes, så "%" og "_" ikke bliver jokertegn.
        var pattern = "%" + EscapeLike(term) + "%";

        const string where =
            "is_active AND (name ILIKE @pattern ESCAPE '\\' OR description ILIKE @pattern ESCAPE '\\')";

        await using var conn = await _db.OpenConnectionAsync(ct);

        var pageSql = "SELECT " + Columns + " FROM products WHERE " + where +
                      " ORDER BY " + OrderByWhitelist[sort] + " LIMIT @limit OFFSET @offset"; // nosemgrep
        await using var cmd = new NpgsqlCommand(pageSql, conn);
        cmd.Parameters.AddWithValue("@pattern", pattern);
        cmd.Parameters.AddWithValue("@limit", pageSize);
        cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
        var items = await ReadProductsAsync(cmd, ct);

        var countSql = "SELECT COUNT(*) FROM products WHERE " + where; // nosemgrep
        await using var countCmd = new NpgsqlCommand(countSql, conn);
        countCmd.Parameters.AddWithValue("@pattern", pattern);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        return new PagedResult<Product> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static async Task<List<Product>> ReadProductsAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var list = new List<Product>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Product
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                Price = reader.GetDecimal(3),
                ImageUrl = reader.GetString(4),
                StockQuantity = reader.GetInt32(5),
                IsActive = reader.GetBoolean(6),
            });
        }
        return list;
    }
}