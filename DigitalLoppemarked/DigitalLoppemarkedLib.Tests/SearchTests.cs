using DigitalLoppemarkedLib.Models;
using DigitalLoppemarkedLib.Service;

namespace DigitalLoppemarkedLib.Tests;

// Covers the acceptance criteria of user story 7 (Søgning / secure search).
public class SearchTests
{
    private static ProductService NewService() => new();

    // ---- 1. Functional: relevant results by name and keyword ----------------

    [Fact]
    public async Task Search_ByName_IsCaseInsensitiveAndMatches()
    {
        var service = NewService();

        var result = await service.SearchAsync("MOUSE", page: 1, pageSize: 6, sort: ProductSort.NameAsc);

        Assert.Contains(result.Items, p => p.Name == "Wireless Mouse");
    }

    [Fact]
    public async Task Search_ByDescriptionKeyword_Matches()
    {
        var service = NewService();

        // "ergonomic" only appears in the Wireless Mouse description, not its name.
        var result = await service.SearchAsync("ergonomic", page: 1, pageSize: 6, sort: ProductSort.NameAsc);

        Assert.Contains(result.Items, p => p.Name == "Wireless Mouse");
    }

    [Fact]
    public async Task Search_ForNonExistentProduct_ReturnsNoResults()
    {
        var service = NewService();

        var result = await service.SearchAsync("nonexistentgizmo", page: 1, pageSize: 6, sort: ProductSort.NameAsc);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    // ---- 2. Security: SQLi / XSS payloads are neutralised -------------------

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("'; DROP TABLE Products; --")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("name=1; return true")]
    public async Task Search_WithInjectionOrXssPayload_ReturnsNoResultsAndDoesNotThrow(string payload)
    {
        var service = NewService();

        var result = await service.SearchAsync(payload, page: 1, pageSize: 6, sort: ProductSort.NameAsc);

        // Disallowed characters fail validation, so the payload matches nothing.
        Assert.Equal(0, result.TotalCount);
    }

    // ---- 2b. Length limiting ------------------------------------------------

    [Fact]
    public async Task Search_WithOverlongTerm_ReturnsNoResults()
    {
        var service = NewService();
        var tooLong = new string('a', ProductService.MaxSearchLength + 1);

        var result = await service.SearchAsync(tooLong, page: 1, pageSize: 6, sort: ProductSort.NameAsc);

        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Search_WithTermAtMaxLength_DoesNotThrow()
    {
        var service = NewService();
        var atLimit = new string('a', ProductService.MaxSearchLength);

        var result = await service.SearchAsync(atLimit, page: 1, pageSize: 6, sort: ProductSort.NameAsc);

        Assert.NotNull(result); // valid length: handled normally, no exception
    }

    // ---- 3. Pagination parameters are validated and bounded -----------------

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Search_WithInvalidPage_ClampsToFirstPage(int page)
    {
        var service = NewService();

        var result = await service.SearchAsync("e", page, pageSize: 2, sort: ProductSort.NameAsc);

        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task Search_WithExcessivePageSize_IsClampedToMaximum()
    {
        var service = NewService();

        var result = await service.SearchAsync("e", page: 1, pageSize: 100_000, sort: ProductSort.NameAsc);

        Assert.True(result.PageSize <= 50, "page size should be clamped to a safe maximum");
    }

    [Fact]
    public async Task Search_PaginatesResults()
    {
        var service = NewService();

        // "e" appears in many product names/descriptions; force multiple pages.
        var page1 = await service.SearchAsync("e", page: 1, pageSize: 2, sort: ProductSort.NameAsc);

        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.TotalCount > 2);
        Assert.True(page1.TotalPages >= 2);
    }

    // ---- 4. Hidden/inactive products are not reachable via search -----------

    [Fact]
    public async Task Search_DoesNotReturnInactiveProducts()
    {
        var service = NewService();

        var result = await service.SearchAsync("Discontinued", page: 1, pageSize: 6, sort: ProductSort.NameAsc);

        Assert.Equal(0, result.TotalCount);
        Assert.DoesNotContain(result.Items, p => p.Id == 9);
    }
}
