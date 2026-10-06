using Core.DDD.Models;
using FluentAssertions;

namespace Elmanhg.Tests.Core.DDD;

public sealed class PageCalculatorTests
{
    [Fact]
    public void Offset_MaxPageNumber_DoesNotOverflow()
    {
        var offset = PageCalculator.Offset(int.MaxValue, 100);

        offset.Should().Be(((long)int.MaxValue - 1) * 100).And.BePositive();
    }

    [Fact]
    public void Offset_FirstPage_IsZero()
    {
        var offset = PageCalculator.Offset(1, 20);

        offset.Should().Be(0);
    }

    [Theory]
    [InlineData(0L, 20, 0L)]
    [InlineData(1L, 20, 1L)]
    [InlineData(20L, 20, 1L)]
    [InlineData(21L, 20, 2L)]
    [InlineData(5_000_000_000L, 1, 5_000_000_000L)]
    public void TotalPages_Counts_RoundsUp(long totalItems, int pageSize, long expected)
    {
        var totalPages = PageCalculator.TotalPages(totalItems, pageSize);

        totalPages.Should().Be(expected);
    }

    [Fact]
    public void TotalPages_NonPositivePageSize_ReturnsZero()
    {
        var totalPages = PageCalculator.TotalPages(10, 0);

        totalPages.Should().Be(0);
    }

    [Fact]
    public void IsPastEnd_OffsetBeyondIntRange_ReturnsTrue()
    {
        var isPastEnd = PageCalculator.IsPastEnd((long)int.MaxValue + 1, long.MaxValue);

        isPastEnd.Should().BeTrue();
    }

    [Theory]
    [InlineData(0L, 0L, true)]
    [InlineData(0L, 1L, false)]
    [InlineData(20L, 20L, true)]
    [InlineData(19L, 20L, false)]
    public void IsPastEnd_Offsets_ComparedToTotal(long offset, long totalItems, bool expected)
    {
        var isPastEnd = PageCalculator.IsPastEnd(offset, totalItems);

        isPastEnd.Should().Be(expected);
    }
}
