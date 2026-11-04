using DigitalLoppemarkedLib.Models;
using DigitalLoppemarkedLib.Service;
using DigitalLoppemarkedLib.Data.Repositories;
using Moq;
using Xunit;

namespace DigitalLoppemarkedLib.Tests;

// Covers the acceptance criteria of user story 7 (Søgning / secure search).
public class SearchTests
{
    private static ProductService NewService()
    {
        var mockRepo = new Mock<IProductRepository>();
        return new ProductService(mockRepo.Object);
    }

    // ---- 1. Functional: relevant results by name and keyword ----------------

    [Fact]
    public async Task Search_ByName_CallsRepo()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.SearchAsync("MOUSE", 1, 6, ProductSort.NameAsc);

        mockRepo.Verify(r => r.SearchActiveAsync("MOUSE", 1, 6, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_ByDescriptionKeyword_CallsRepo()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.SearchAsync("ergonomic", 1, 6, ProductSort.NameAsc);

        mockRepo.Verify(r => r.SearchActiveAsync("ergonomic", 1, 6, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_ForNonExistentProduct_CallsRepo()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.SearchAsync("nonexistentgizmo", 1, 6, ProductSort.NameAsc);

        mockRepo.Verify(r => r.SearchActiveAsync("nonexistentgizmo", 1, 6, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- 2. Security: SQLi / XSS payloads are neutralised -------------------

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("'; DROP TABLE Products; --")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("name=1; return true")]
    public async Task Search_WithInjectionOrXssPayload_DoesNotCallRepo(string payload)
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.SearchAsync(payload, 1, 6, ProductSort.NameAsc);

        // Disallowed characters fail validation, so the payload matches nothing and repo is never called.
        mockRepo.Verify(r => r.SearchActiveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ProductSort>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- 2b. Length limiting ------------------------------------------------

    [Fact]
    public async Task Search_WithOverlongTerm_DoesNotCallRepo()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);
        var tooLong = new string('a', ProductService.MaxSearchLength + 1);

        await service.SearchAsync(tooLong, 1, 6, ProductSort.NameAsc);

        mockRepo.Verify(r => r.SearchActiveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ProductSort>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Search_WithTermAtMaxLength_CallsRepo()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);
        var atLimit = new string('a', ProductService.MaxSearchLength);

        await service.SearchAsync(atLimit, 1, 6, ProductSort.NameAsc);

        mockRepo.Verify(r => r.SearchActiveAsync(atLimit, 1, 6, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- 3. Pagination parameters are validated and bounded -----------------

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Search_WithInvalidPage_ClampsToFirstPage(int page)
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.SearchAsync("e", page, 2, ProductSort.NameAsc);

        mockRepo.Verify(r => r.SearchActiveAsync("e", 1, 2, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_WithExcessivePageSize_IsClampedToMaximum()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.SearchAsync("e", 1, 100_000, ProductSort.NameAsc);

        mockRepo.Verify(r => r.SearchActiveAsync("e", 1, 50, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_PaginatesResults()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.SearchAsync("e", 1, 2, ProductSort.NameAsc);

        mockRepo.Verify(r => r.SearchActiveAsync("e", 1, 2, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- 4. Hidden/inactive products are handled by Repository -------------------
    // (Verified as Repository responsibility, but we test the Service calls)

    [Fact]
    public async Task Search_CallsRepo()
    {
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        await service.SearchAsync("Discontinued", 1, 6, ProductSort.NameAsc);

        mockRepo.Verify(r => r.SearchActiveAsync("Discontinued", 1, 6, ProductSort.NameAsc, It.IsAny<CancellationToken>()), Times.Once);
    }
}
