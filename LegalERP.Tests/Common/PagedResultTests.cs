using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using LegalERP.Application.Common;
using Xunit;

namespace LegalERP.Tests.Common;

public class PagedResultTests
{
    [Fact]
    public void PaginationParams_DefaultValues_AreCorrect()
    {
        var p = new PaginationParams();

        p.PageNumber.Should().Be(1);
        p.PageSize.Should().Be(25);
        p.Skip.Should().Be(0);
        p.Take.Should().Be(25);
    }

    [Theory]
    [InlineData(1, 10, 0, 10)]
    [InlineData(2, 10, 10, 10)]
    [InlineData(3, 20, 40, 20)]
    [InlineData(-1, 20, 0, 20)] // clamped/non-negative skip
    public void PaginationParams_SkipAndTake_CalculateAccurately(int page, int size, int expectedSkip, int expectedTake)
    {
        var p = new PaginationParams { PageNumber = page, PageSize = size };

        p.Skip.Should().Be(expectedSkip);
        p.Take.Should().Be(expectedTake);
    }

    [Theory]
    [InlineData(200, 100)] // Clamped to 100 max
    [InlineData(0, 1)]     // Clamped to 1 min
    [InlineData(-5, 1)]    // Clamped to 1 min
    public void PaginationParams_Take_IsClampedBetween1And100(int inputSize, int expectedTake)
    {
        var p = new PaginationParams { PageSize = inputSize };

        p.Take.Should().Be(expectedTake);
    }

    [Theory]
    [InlineData(0, 25, 0)]
    [InlineData(25, 25, 1)]
    [InlineData(26, 25, 2)]
    [InlineData(100, 25, 4)]
    [InlineData(101, 25, 5)]
    public void PagedResult_TotalPages_CalculatesAccurately(int totalCount, int pageSize, int expectedTotalPages)
    {
        var result = new PagedResult<string>([], totalCount, 1, pageSize);

        result.TotalPages.Should().Be(expectedTotalPages);
    }

    [Theory]
    [InlineData(1, 5, false, true)]
    [InlineData(3, 5, true, true)]
    [InlineData(5, 5, true, false)]
    [InlineData(1, 1, false, false)]
    [InlineData(1, 0, false, false)]
    public void PagedResult_NavigationBooleans_AreAccurate(int pageNumber, int totalPages, bool expectedHasPrev, bool expectedHasNext)
    {
        var totalCount = totalPages * 10;
        var result = new PagedResult<string>([], totalCount, pageNumber, 10);

        result.HasPreviousPage.Should().Be(expectedHasPrev);
        result.HasNextPage.Should().Be(expectedHasNext);
    }

    [Fact]
    public void PagedResult_JsonSerializationAndDeserialization_RoundTripsCorrectly()
    {
        var original = new PagedResult<string>(["Case 1", "Case 2"], 50, 2, 25);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var json = JsonSerializer.Serialize(original, jsonOptions);
        var deserialized = JsonSerializer.Deserialize<PagedResult<string>>(json, jsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Items.Should().BeEquivalentTo(new[] { "Case 1", "Case 2" });
        deserialized.TotalCount.Should().Be(50);
        deserialized.PageNumber.Should().Be(2);
        deserialized.PageSize.Should().Be(25);
        deserialized.TotalPages.Should().Be(2);
        deserialized.HasPreviousPage.Should().BeTrue();
        deserialized.HasNextPage.Should().BeFalse();
    }
}
