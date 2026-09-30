using Elmanhg.Application.Dashboard.Shared;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Dashboard.Shared;

public sealed class DashboardCacheKeyTests
{
    [Fact]
    public void For_IncludesCardRangeAndSubject()
    {
        var subjectId = Guid.Parse("0b6f7d8e-1c2a-4e3b-9a5d-6f7e8d9c0a1b");

        DashboardCacheKey.For("solve-rate", new DateOnly(2021, 6, 1), new DateOnly(2021, 6, 30), subjectId)
            .Should().Be("dashboard:solve-rate:2021-06-01:2021-06-30:0b6f7d8e1c2a4e3b9a5d6f7e8d9c0a1b");
    }

    [Fact]
    public void For_MissingFilters_LeavesTheirSlotsEmpty()
    {
        DashboardCacheKey.For("funnel", null, null, null).Should().Be("dashboard:funnel:::");
    }
}
