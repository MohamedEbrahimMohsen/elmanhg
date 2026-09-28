using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Lessons.UpdateLesson;

public sealed class UpdateLessonValidator : AbstractValidator<UpdateLessonCommand>
{
    public UpdateLessonValidator(IOptions<ContentOptions> contentOptions)
    {
        var options = contentOptions.Value;

        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
        RuleFor(x => x.Name)
            .ValidateRequired(ErrorCodes.LessonNameRequired)
            .ValidateMaxLength(options.LessonNameMaxLength, ErrorCodes.LessonNameTooLong);
        RuleFor(x => x.Explanation).ValidateMaxLength(options.LessonExplanationMaxLength, ErrorCodes.LessonExplanationTooLong);
        RuleFor(x => x.Summary).ValidateMaxLength(options.LessonSummaryMaxLength, ErrorCodes.LessonSummaryTooLong);
        RuleFor(x => x.VideoUrl)
            .ValidateUrl(ErrorCodes.LessonVideoUrlInvalid)
            .ValidateMaxLength(options.LessonVideoUrlMaxLength, ErrorCodes.LessonVideoUrlTooLong)
            .When(x => !string.IsNullOrWhiteSpace(x.VideoUrl));
        RuleFor(x => x.Objectives).ValidateListMaxItems(options.LessonObjectivesMaxCount, ErrorCodes.LessonObjectivesTooMany);
        RuleFor(x => x.Objectives)
            .Must(objectives => objectives is null || objectives.Where(x => x.Id.HasValue).GroupBy(x => x.Id).All(x => x.Count() == 1))
            .WithErrorCode(ErrorCodes.LessonObjectiveDuplicate);
        RuleForEach(x => x.Objectives).ChildRules(objective => objective.RuleFor(x => x.Text)
            .ValidateRequired(ErrorCodes.LessonObjectiveTextRequired)
            .ValidateMaxLength(options.LessonObjectiveMaxLength, ErrorCodes.LessonObjectiveTextTooLong));
    }
}
