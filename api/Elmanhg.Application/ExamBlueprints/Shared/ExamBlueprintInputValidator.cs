using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.ExamBlueprints.Shared;

public sealed class ExamBlueprintInputValidator : AbstractValidator<ExamBlueprintInput>
{
    // Exam scores are out of 100 (PRD §7.4), so a pass mark is a percentage.
    private const int PassMarkMax = 100;
    // Difficulty mix values are whole percentages of the exam.
    private const int PercentMax = 100;

    public ExamBlueprintInputValidator(IOptions<ExamBlueprintsOptions> examBlueprintsOptions)
    {
        var options = examBlueprintsOptions.Value;

        RuleFor(x => x.TypeCounts)
            .Must(list => list is not null && list.Where(x => x is not null).Sum(x => (long)x.Count) >= 1)
            .WithErrorCode(ErrorCodes.ExamBlueprintEmpty)
            .Must(list => list is null || list.Where(x => x is not null).Sum(x => (long)x.Count) <= options.MaxQuestionCount)
            .WithErrorCode(ErrorCodes.ExamBlueprintTooLarge)
            .Must(list => list is null || list.Where(x => x is not null && x.Type.HasValue).GroupBy(x => x.Type).All(x => x.Count() == 1))
            .WithErrorCode(ErrorCodes.ExamBlueprintTypeDuplicate);
        RuleForEach(x => x.TypeCounts).NotNull().WithErrorCode(ErrorCodes.ExamBlueprintTypeCountRequired);
        RuleForEach(x => x.TypeCounts).ChildRules(item =>
        {
            item.RuleFor(x => x.Type)
                .ValidateRequired(ErrorCodes.QuestionTypeRequired)
                .IsInEnum()
                .WithErrorCode(ErrorCodes.QuestionTypeInvalid);
            item.RuleFor(x => x.Count).ValidateRange(0, options.MaxQuestionCount, ErrorCodes.ExamBlueprintCountInvalid);
        });
        RuleFor(x => x.DifficultyMix!).ChildRules(mix =>
        {
            mix.RuleFor(x => x.EasyPercent).ValidateRange(0, PercentMax, ErrorCodes.ExamBlueprintDifficultyMixInvalid);
            mix.RuleFor(x => x.MediumPercent).ValidateRange(0, PercentMax, ErrorCodes.ExamBlueprintDifficultyMixInvalid);
            mix.RuleFor(x => x.HardPercent).ValidateRange(0, PercentMax, ErrorCodes.ExamBlueprintDifficultyMixInvalid);
            mix.RuleFor(x => x)
                .Must(x => x.EasyPercent + x.MediumPercent + x.HardPercent == PercentMax)
                .WithErrorCode(ErrorCodes.ExamBlueprintDifficultyMixInvalid);
        }).When(x => x.DifficultyMix is not null);
        RuleFor(x => x.TimeLimitMinutes.GetValueOrDefault())
            .ValidateRange(1, options.MaxTimeLimitMinutes, ErrorCodes.ExamBlueprintTimeLimitInvalid)
            .When(x => x.TimeLimitMinutes.HasValue)
            .OverridePropertyName(nameof(ExamBlueprintInput.TimeLimitMinutes));
        RuleFor(x => x.PassMark).ValidateRange(1, PassMarkMax, ErrorCodes.ExamBlueprintPassMarkInvalid);
    }
}
