using DigitalLoppemarkedLib.Models;
using DigitalLoppemarkedLib.Service;
using DigitalLoppemarkedLib.Data.Repositories;
using Moq;

namespace DigitalLoppemarkedLib.Tests;

// Covers the acceptance criteria of user story 3 (Gennemse produkter):
public class ProductBrowseTests
{
    private static ProductService NewService()
    {
        var mockRepo = new Mock<IProductRepository>();
        // Mock basic behavior to prevent null references if needed,
        // though these tests primarily check service logic like paging normalization.
        return new ProductService(mockRepo.Object);
    }

    // ---- 1. List renders correctly for valid parameters ---------------------

    [Fact]
    public async Task GetProducts_WithValidParameters_CallsRepoWithCorrectParams()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.GetProductsAsync(page: 1, pageSize: 6, sort: ProductSort.NameAsc);

        mockRepo.Verify(r => r.GetActivePageAsync(1, 6, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProducts_SortByPriceAscending_CallsRepoWithPriceSort()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.GetProductsAsync(page: 1, pageSize: 50, sort: ProductSort.PriceAsc);

        mockRepo.Verify(r => r.GetActivePageAsync(1, 50, ProductSort.PriceAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProducts_SecondPage_CallsRepoWithCorrectPage()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.GetProductsAsync(page: 2, pageSize: 6, sort: ProductSort.NameAsc);

        mockRepo.Verify(r => r.GetActivePageAsync(2, 6, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- 2. SQLi / XSS strings in parameters are neutralised ----------------

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("'; DROP TABLE Products; --")]
    [InlineData("<script>alert(1)</script>")]
    public async Task Search_WithInjectionPayload_DoesNotCallRepo(string payload)
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.SearchAsync(payload, 1, 6, ProductSort.NameAsc);

        // Payloads should be blocked by AllowedSearch regex, so repo is never called.
        mockRepo.Verify(r => r.SearchActiveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ProductSort>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Search_WithValidTerm_CallsRepo()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.SearchAsync("mouse", 1, 6, ProductSort.NameAsc);

        mockRepo.Verify(r => r.SearchActiveAsync("mouse", 1, 6, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- 3. Pagination / sort parameters are validated and bounded ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetProducts_WithInvalidPage_ClampsToFirstPage(int page)
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.GetProductsAsync(page, pageSize: 6, sort: ProductSort.NameAsc);

        mockRepo.Verify(r => r.GetActivePageAsync(1, 6, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProducts_WithExcessivePageSize_IsClampedToMaximum()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.GetProductsAsync(page: 1, pageSize: 100_000, sort: ProductSort.NameAsc);

        mockRepo.Verify(r => r.GetActivePageAsync(1, 50, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProducts_WithInvalidPageSize_UsesDefault()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.GetProductsAsync(page: 1, pageSize: 0, sort: ProductSort.NameAsc);

        mockRepo.Verify(r => r.GetActivePageAsync(1, 6, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- 4. Hidden/inactive products are handled by Repository ----------------------
    // (Note: These are now verified as Repository responsibilities, but we test the Service calls)

    [Fact]
    public async Task GetProductById_CallsRepo()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.GetProductByIdAsync(1);

        mockRepo.Verify(r => r.GetActiveByIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
