using FluentValidation;

namespace Core.Validation.Extensions;

// Instants form a half-open range, so the end must be strictly after the start; calendar dates count whole inclusive days.
public static class DateRangeValidationExtensions
{
    private const string RangeMessage = "The end must be after the start.";
    private const string TooLongMessage = "The range is too long.";

    public static IRuleBuilderOptions<T, T> ValidateDateRange<T>(this IRuleBuilder<T, T> ruleBuilder, Func<T, DateTimeOffset?> from, Func<T, DateTimeOffset?> to, string? errorCode = null, TimeSpan? maxSpan = null, string? maxSpanErrorCode = null)
        => ruleBuilder
            .Must(x => from(x) is not { } start || to(x) is not { } end || start < end)
            .WithMessage(RangeMessage)
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationDateRange)
            .Must(x => maxSpan is not { } limit || from(x) is not { } start || to(x) is not { } end || start >= end || end - start <= limit)
            .WithMessage(TooLongMessage)
            .WithErrorCode(maxSpanErrorCode ?? ValidationErrors.ValidationDateRangeTooLong);

    public static IRuleBuilderOptions<T, T> ValidateDateRange<T>(this IRuleBuilder<T, T> ruleBuilder, Func<T, DateOnly?> from, Func<T, DateOnly?> to, string? errorCode = null, int? maxDays = null, string? maxDaysErrorCode = null)
        => ruleBuilder
            .Must(x => from(x) is not { } start || to(x) is not { } end || end >= start)
            .WithMessage(RangeMessage)
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationDateRange)
            .Must(x => maxDays is not { } limit || from(x) is not { } start || to(x) is not { } end || end < start || end.DayNumber - start.DayNumber + 1 <= limit)
            .WithMessage(TooLongMessage)
            .WithErrorCode(maxDaysErrorCode ?? ValidationErrors.ValidationDateRangeTooLong);
}
