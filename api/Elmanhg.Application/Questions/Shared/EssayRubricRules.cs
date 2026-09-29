using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Schemas;

namespace Elmanhg.Application.Questions.Shared;

public static class EssayRubricRules
{
    // A scale needs a zero level and a full-points level.
    private const int MinimumLevels = 2;
    private const int MinimumCriteria = 1;

    public static void AddErrors(List<string> errors, List<RubricCriterion>? criteria, ContentOptions options)
    {
        var list = criteria ?? [];
        var ids = list
            .Select(x => x?.Id)
            .ToList();
        var levels = list
            .SelectMany(x => x?.Levels ?? [])
            .ToList();
        QuestionSchemaReader.AddIf(errors, list.Count < MinimumCriteria || list.Count > options.QuestionRubricCriteriaMaxCount, ErrorCodes.QuestionRubricCriteriaCountInvalid);
        QuestionSchemaReader.AddIf(errors, ids.Any(x => !QuestionSchemaReader.IsValidId(x)), ErrorCodes.QuestionRubricCriterionIdInvalid);
        QuestionSchemaReader.AddIf(errors, ids.Distinct(StringComparer.Ordinal).Count() != ids.Count, ErrorCodes.QuestionRubricCriterionIdDuplicate);
        QuestionSchemaReader.AddIf(errors, list.Any(x => string.IsNullOrWhiteSpace(x?.Title)), ErrorCodes.QuestionRubricCriterionTitleRequired);
        QuestionSchemaReader.AddIf(errors, list.Any(x => IsTooLong(x?.Title, options.QuestionRubricTextMaxLength) || IsTooLong(x?.Description, options.QuestionRubricTextMaxLength)) || levels.Any(x => IsTooLong(x?.Description, options.QuestionRubricTextMaxLength)), ErrorCodes.QuestionRubricTextTooLong);
        QuestionSchemaReader.AddIf(errors, list.Any(x => x?.Points is null || x.Points < 1 || x.Points > options.QuestionRubricPointsMax), ErrorCodes.QuestionRubricPointsInvalid);
        QuestionSchemaReader.AddIf(errors, list.Any(x => (x?.Levels?.Count ?? 0) < MinimumLevels || (x?.Levels?.Count ?? 0) > options.QuestionRubricLevelsMaxCount), ErrorCodes.QuestionRubricLevelsCountInvalid);
        QuestionSchemaReader.AddIf(errors, levels.Any(x => string.IsNullOrWhiteSpace(x?.Description)), ErrorCodes.QuestionRubricLevelDescriptionRequired);
        QuestionSchemaReader.AddIf(errors, list.Any(x => x?.Points is { } points && points >= 1 && points <= options.QuestionRubricPointsMax && !IsFullScale(x.Levels ?? [], points)), ErrorCodes.QuestionRubricLevelPointsInvalid);
    }

    private static bool IsFullScale(List<RubricLevel> levels, int points)
    {
        var values = levels
            .Select(x => x?.Points)
            .ToList();
        return values.All(x => x is { } value && value >= 0 && value <= points)
            && values.Distinct().Count() == values.Count
            && values.Contains(0)
            && values.Contains(points);
    }

    private static bool IsTooLong(string? text, int max) => text is not null && text.Trim().Length > max;
}
