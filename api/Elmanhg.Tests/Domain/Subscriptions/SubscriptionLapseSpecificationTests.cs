using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Subscriptions;

public sealed class SubscriptionLapseSpecificationTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromDays(3);
    private static readonly DateTimeOffset PeriodEnd = SubscriptionBuilder.DefaultStart.AddMonths(1);

    public static TheoryData<SubscriptionStatus, int> Grid()
    {
        var data = new TheoryData<SubscriptionStatus, int>();
        foreach (var status in Enum.GetValues<SubscriptionStatus>())
        {
            foreach (var hoursAfterEnd in new[] { -24, 0, 24, 72, 96 })
            {
                data.Add(status, hoursAfterEnd);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Grid))]
    public void DueAt_StatusAndTimeGrid_AgreesWithLapse(SubscriptionStatus status, int hoursAfterEnd)
    {
        var now = PeriodEnd.AddHours(hoursAfterEnd);

        var due = SubscriptionLapseSpecification.DueAt(now, Grace, []).Compile()(new SubscriptionBuilder().InStatus(status).Build());

        due.Should().Be(new SubscriptionBuilder().InStatus(status).Build().Lapse(now, Grace));
    }

    [Fact]
    public void DueAt_ExcludedId_IsFalse()
    {
        var subscription = new SubscriptionBuilder().Build();
        var now = PeriodEnd.AddDays(1);

        var due = SubscriptionLapseSpecification.DueAt(now, Grace, [subscription.Id]).Compile()(subscription);

        (due, SubscriptionLapseSpecification.DueAt(now, Grace, []).Compile()(subscription)).Should().Be((false, true));
    }
}
