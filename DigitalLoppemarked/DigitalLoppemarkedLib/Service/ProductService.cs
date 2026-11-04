// Service/ProductService.cs
using System.Text.RegularExpressions;
using DigitalLoppemarkedLib.Data.Repositories;
using DigitalLoppemarkedLib.Models;

namespace DigitalLoppemarkedLib.Service;

public class ProductService
{
    private const int DefaultPageSize = 6;
    private const int MaxPageSize = 50;
    public const int MaxSearchLength = 100;

    // Kun bogstaver, tal, mellemrum og bindestreg (æøå er med via \p{L}).
    private static readonly Regex AllowedSearch = new(@"^[\p{L}\p{N}\s\-]+$", RegexOptions.Compiled);

    private readonly IProductRepository _products;
    public ProductService(IProductRepository products) => _products = products;

    public Task<PagedResult<Product>> GetProductsAsync(int page, int pageSize, ProductSort sort, CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        return _products.GetActivePageAsync(page, pageSize, sort, ct);
    }

    public Task<Product?> GetProductByIdAsync(int id, CancellationToken ct = default) =>
        _products.GetActiveByIdAsync(id, ct);

    public Task<PagedResult<Product>> SearchAsync(string term, int page, int pageSize, ProductSort sort, CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        term = (term ?? "").Trim();

        // Ugyldig søgning giver tomt resultat frem for exception (samme adfærd som før).
        if (term.Length == 0 || term.Length > MaxSearchLength || !AllowedSearch.IsMatch(term))
            return Task.FromResult(new PagedResult<Product> { Page = page, PageSize = pageSize });

        return _products.SearchActiveAsync(term, page, pageSize, sort, ct);
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
        (Math.Max(1, page), pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize));
}