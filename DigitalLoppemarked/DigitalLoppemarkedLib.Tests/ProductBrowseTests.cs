using DigitalLoppemarkedLib.Models;
using DigitalLoppemarkedLib.Service;

namespace DigitalLoppemarkedLib.Tests;

// Covers the acceptance criteria of user story 3 (Gennemse produkter):
public class ProductBrowseTests
{
    private static ProductService NewService() => new();

    // ---- 1. List renders correctly for valid parameters ---------------------

    [Fact]
    public async Task GetProducts_WithValidParameters_ReturnsFirstPageOfActiveProducts()
    {
        var service = NewService();

        var result = await service.GetProductsAsync(page: 1, pageSize: 6, sort: ProductSort.NameAsc);

        Assert.Equal(1, result.Page);
        Assert.Equal(6, result.PageSize);
        Assert.Equal(6, result.Items.Count);           // full first page
        Assert.Equal(8, result.TotalCount);            // 8 active products (9 total, 1 inactive)
        Assert.Equal(2, result.TotalPages);
        Assert.All(result.Items, p => Assert.True(p.IsActive));
    }

    [Fact]
    public async Task GetProducts_SortByPriceAscending_OrdersByPrice()
    {
        var service = NewService();

        var result = await service.GetProductsAsync(page: 1, pageSize: 50, sort: ProductSort.PriceAsc);

        var prices = result.Items.Select(p => p.Price).ToList();
        Assert.Equal(prices.OrderBy(p => p).ToList(), prices);
    }

    [Fact]
    public async Task GetProducts_SecondPage_ReturnsRemainingItems()
    {
        var service = NewService();

        var result = await service.GetProductsAsync(page: 2, pageSize: 6, sort: ProductSort.NameAsc);

        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.Items.Count);           // 8 active - 6 on page 1
    }

    // ---- 2. SQLi / XSS strings in parameters are neutralised ----------------

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("'; DROP TABLE Products; --")]
    [InlineData("<script>alert(1)</script>")]
    public async Task Search_WithInjectionPayload_ReturnsNoMatchesAndDoesNotThrow(string payload)
    {
        var service = NewService();

        var results = await service.SearchAsync(payload);

        // The payload is treated as an ordinary (non-matching) search term, never executed.
        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_WithValidTerm_IsCaseInsensitiveAndMatches()
    {
        var service = NewService();

        var results = await service.SearchAsync("mouse");

        Assert.Contains(results, p => p.Name == "Wireless Mouse");
    }

    // ---- 3. Pagination / sort parameters are validated and bounded ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetProducts_WithInvalidPage_ClampsToFirstPage(int page)
    {
        var service = NewService();

        var result = await service.GetProductsAsync(page, pageSize: 6, sort: ProductSort.NameAsc);

        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task GetProducts_WithExcessivePageSize_IsClampedToMaximum()
    {
        var service = NewService();

        var result = await service.GetProductsAsync(page: 1, pageSize: 100_000, sort: ProductSort.NameAsc);

        Assert.True(result.PageSize <= 50, "page size should be clamped to a safe maximum");
    }

    [Fact]
    public async Task GetProducts_WithInvalidPageSize_UsesDefault()
    {
        var service = NewService();

        var result = await service.GetProductsAsync(page: 1, pageSize: 0, sort: ProductSort.NameAsc);

        Assert.True(result.PageSize > 0);
    }

    // ---- 4. Hidden/inactive products are not reachable ----------------------

    [Fact]
    public async Task GetProducts_DoesNotIncludeInactiveProducts()
    {
        var service = NewService();

        var result = await service.GetProductsAsync(page: 1, pageSize: 50, sort: ProductSort.NameAsc);

        Assert.DoesNotContain(result.Items, p => p.Id == 9);
        Assert.All(result.Items, p => Assert.True(p.IsActive));
    }

    [Fact]
    public async Task GetProductById_ForInactiveProduct_ReturnsNull()
    {
        var service = NewService();

        // Id 9 is the seeded inactive "Discontinued Gadget".
        var product = await service.GetProductByIdAsync(9);

        Assert.Null(product);
    }

    [Fact]
    public async Task GetProductById_ForActiveProduct_ReturnsProduct()
    {
        var service = NewService();

        var product = await service.GetProductByIdAsync(1);

        Assert.NotNull(product);
        Assert.True(product!.IsActive);
    }

    [Fact]
    public async Task Search_DoesNotReturnInactiveProducts()
    {
        var service = NewService();

        var results = await service.SearchAsync("Discontinued");

        Assert.Empty(results);
    }

    [Fact]
    public async Task ReduceStock_ForInactiveProduct_Fails()
    {
        var service = NewService();

        var ok = await service.ReduceStockAsync(productId: 9, quantity: 1);

        Assert.False(ok);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ReduceStock_WithNonPositiveQuantity_Fails(int quantity)
    {
        var service = NewService();

        var ok = await service.ReduceStockAsync(productId: 1, quantity);

        Assert.False(ok);
    }
}
