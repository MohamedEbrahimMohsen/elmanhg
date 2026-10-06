using Core.DDD.Models;
using FluentValidation;

namespace Core.Validation.Extensions;

public static class PagingValidationExtensions
{
    public static IRuleBuilderOptions<T, T> ValidatePaging<T>(this IRuleBuilder<T, T> ruleBuilder, Func<T, int> pageNumber, Func<T, int> pageSize, int maxPageSize, string? pageNumberErrorCode = null, string? pageSizeErrorCode = null)
        => ruleBuilder
            .Must(x => pageNumber(x) >= 1)
            .WithMessage("Page number must be at least 1.")
            .WithErrorCode(pageNumberErrorCode ?? ValidationErrors.ValidationPageNumber)
            .Must(x => pageNumber(x) < 1 || PageCalculator.Offset(pageNumber(x), pageSize(x)) <= int.MaxValue)
            .WithMessage("Page number is beyond the last addressable page.")
            .WithErrorCode(pageNumberErrorCode ?? ValidationErrors.ValidationPageNumber)
            .Must(x => pageSize(x) >= 1 && pageSize(x) <= maxPageSize)
            .WithMessage($"Page size must be between 1 and {maxPageSize}.")
            .WithErrorCode(pageSizeErrorCode ?? ValidationErrors.ValidationPageSize);
}
