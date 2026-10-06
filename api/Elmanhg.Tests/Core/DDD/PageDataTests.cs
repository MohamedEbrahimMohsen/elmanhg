using Core.DDD.Models;
using FluentAssertions;

namespace Elmanhg.Tests.Core.DDD;

public sealed class PageDataTests
{
    private static readonly PageData<int> Page = new() { Items = [1, 2, 3], PageNumber = 2, PageSize = 3, TotalItems = 8, TotalPages = 3 };

    [Fact]
    public void Map_Items_ProjectsEachItemInOrder()
    {
        var mapped = Page.Map(x => $"item-{x}");

        mapped.Items.Should().Equal("item-1", "item-2", "item-3");
    }

    [Fact]
    public void Map_PagingFields_CopiedUnchanged()
    {
        var mapped = Page.Map(x => x * 10);

        (mapped.PageNumber, mapped.PageSize, mapped.TotalItems, mapped.TotalPages).Should().Be((2L, 3L, 8L, 3L));
    }
}
