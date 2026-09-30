using Elmanhg.Application.Dashboard.GetPaymentMetrics;
using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Dashboard.GetPaymentMetrics;

public sealed class GetPaymentMetricsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly GetPaymentMetricsHandler _handler;

    public GetPaymentMetricsHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _paymentRepository.GetTotalsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new PaymentTotals());
        _paymentRepository.GetRevenueByDayAsync(Arg.Any<MetricsWindow>(), Arg.Any<CancellationToken>()).Returns([]);
        _handler = new GetPaymentMetricsHandler(_paymentRepository, _timeProvider, Options.Create(new DashboardOptions()), Options.Create(new SubscriptionsOptions { Currency = "EGP" }));
    }

    [Fact]
    public async Task Handle_ComputesNetRevenueInConfiguredCurrency()
    {
        _paymentRepository.GetTotalsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new PaymentTotals { Succeeded = 2, Failed = 1, GrossMinor = 89800, Refunds = 1, RefundedMinor = 69900 });

        var result = await _handler.Handle(new GetPaymentMetricsQuery(null, null), TestContext.Current.CancellationToken);

        result.Succeeded.Should().Be(2);
        result.Failed.Should().Be(1);
        result.Refunds.Should().Be(1);
        result.Revenue.Should().Be(new Money(89800, "EGP"));
        result.Refunded.Should().Be(new Money(69900, "EGP"));
        result.NetRevenue.Should().Be(new Money(19900, "EGP"));
        result.GeneratedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ZeroFillsRevenueByDay()
    {
        var from = new DateOnly(2026, 1, 13);
        var to = new DateOnly(2026, 1, 14);
        _paymentRepository.GetRevenueByDayAsync(Arg.Any<MetricsWindow>(), Arg.Any<CancellationToken>()).Returns([new DailyTotal { Day = to, Value = 19900 }]);

        var result = await _handler.Handle(new GetPaymentMetricsQuery(from, to), TestContext.Current.CancellationToken);

        result.RevenueByDay.Should().Equal(new DailyValueResult(from, 0), new DailyValueResult(to, 19900));
    }
}
