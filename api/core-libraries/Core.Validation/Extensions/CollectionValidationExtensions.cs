using FluentValidation;

namespace Core.Validation.Extensions;

public static class CollectionValidationExtensions
{
    public static IRuleBuilderOptions<T, IList<TItem>> ValidateNotEmptyList<T, TItem>(this IRuleBuilder<T, IList<TItem>> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Must(list => list != null && list.Count > 0)
            .WithMessage("{PropertyName} must contain at least one item.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationRequired);

    public static IRuleBuilderOptions<T, IList<TItem>> ValidateListMaxItems<T, TItem>(this IRuleBuilder<T, IList<TItem>> ruleBuilder, int maxItems, string? errorCode = null)
        => ruleBuilder
            .Must(list => list == null || list.Count <= maxItems)
            .WithMessage($"{{PropertyName}} must not contain more than {maxItems} items.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationListMaxItems);

    public static IRuleBuilderOptions<T, IEnumerable<TItem>?> ValidateDistinct<T, TItem>(this IRuleBuilder<T, IEnumerable<TItem>?> ruleBuilder, string? errorCode = null)
        => ruleBuilder
            .Must(items => items is null || items.Distinct().Count() == items.Count())
            .WithMessage("{PropertyName} must not contain duplicates.")
            .WithErrorCode(errorCode ?? ValidationErrors.ValidationDistinct);
}
