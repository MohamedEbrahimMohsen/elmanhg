using Core.Validation.Extensions;
using FluentAssertions;
using FluentValidation;

namespace Elmanhg.Tests.Core.Validation;

public sealed class DateRangeValidationExtensionsTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 3, 1);

    [Fact]
    public void ValidateDateRange_InstantsInOrder_Passes()
    {
        InstantCodes(Start, Start.AddHours(1)).Should().BeEmpty();
    }

    [Fact]
    public void ValidateDateRange_InstantsEqual_FailsRangeCode()
    {
        InstantCodes(Start, Start).Should().Equal("R");
    }

    [Fact]
    public void ValidateDateRange_InstantMissing_Passes()
    {
        InstantCodes(null, Start).Should().BeEmpty();
    }

    [Fact]
    public void ValidateDateRange_InstantsWiderThanMaxSpan_FailsTooLongCodeOnly()
    {
        InstantCodes(Start, Start.AddDays(10).AddTicks(1)).Should().Equal("L");
    }

    [Fact]
    public void ValidateDateRange_InstantsReversedWithMaxSpan_FailsRangeCodeOnly()
    {
        InstantCodes(Start.AddDays(30), Start).Should().Equal("R");
    }

    [Fact]
    public void ValidateDateRange_DatesSameDay_Passes()
    {
        DateCodes(Day, Day).Should().BeEmpty();
    }

    [Fact]
    public void ValidateDateRange_DatesReversed_FailsRangeCodeOnly()
    {
        DateCodes(Day.AddDays(30), Day).Should().Equal("R");
    }

    [Fact]
    public void ValidateDateRange_DatesOneDayOverMax_FailsTooLongCode()
    {
        DateCodes(Day, Day.AddDays(10)).Should().Equal("L");
    }

    [Fact]
    public void ValidateDateRange_DatesExactlyMaxDays_Passes()
    {
        DateCodes(Day, Day.AddDays(9)).Should().BeEmpty();
    }

    private static List<string> InstantCodes(DateTimeOffset? from, DateTimeOffset? to) => new InstantValidator().Validate(new InstantRange(from, to)).Errors.Select(x => x.ErrorCode).ToList();

    private static List<string> DateCodes(DateOnly? from, DateOnly? to) => new DateValidator().Validate(new DateRange(from, to)).Errors.Select(x => x.ErrorCode).ToList();

    private sealed record InstantRange(DateTimeOffset? From, DateTimeOffset? To);

    private sealed record DateRange(DateOnly? From, DateOnly? To);

    private sealed class InstantValidator : AbstractValidator<InstantRange>
    {
        public InstantValidator()
        {
            RuleFor(x => x).ValidateDateRange(x => x.From, x => x.To, "R", TimeSpan.FromDays(10), "L");
        }
    }

    private sealed class DateValidator : AbstractValidator<DateRange>
    {
        public DateValidator()
        {
            RuleFor(x => x).ValidateDateRange(x => x.From, x => x.To, "R", 10, "L");
        }
    }
}
