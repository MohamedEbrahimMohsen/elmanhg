using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using System.Globalization;

namespace Elmanhg.Application.Avatar.Shared;

public static partial class AvatarAnswerText
{
    public static string? CorrectAnswer(QuestionRevisionSnapshot snapshot, IRichTextExtractor extractor)
    {
        return snapshot.Type switch
        {
            QuestionType.Mcq => OptionText(Read<ChoiceBody>(snapshot.Body), Read<McqGradingSpec>(snapshot.GradingSpec)?.CorrectOptionId, extractor),
            QuestionType.Multi => OptionTexts(Read<ChoiceBody>(snapshot.Body), Read<MultiGradingSpec>(snapshot.GradingSpec)?.CorrectOptionIds, extractor),
            QuestionType.TrueFalse => BooleanText(Read<TrueFalseGradingSpec>(snapshot.GradingSpec)?.CorrectAnswer),
            QuestionType.Fill => Blanks(Read<FillGradingSpec>(snapshot.GradingSpec)?.Blanks?.Select(x => (x.Id, x.AcceptedAnswers?.FirstOrDefault()))),
            QuestionType.Short => ShortText(Read<ShortGradingSpec>(snapshot.GradingSpec)),
            _ => null,
        };
    }

    private static string? ShortText(ShortGradingSpec? spec)
    {
        if (spec?.Value is not { } value)
        {
            return NullIfBlank(spec?.AcceptedAnswers?.FirstOrDefault());
        }

        var text = value.ToString(CultureInfo.InvariantCulture);
        if (spec.Tolerance is { } tolerance && tolerance > 0)
        {
            text += " ± " + tolerance.ToString(CultureInfo.InvariantCulture);
            if (spec.ToleranceMode == ToleranceMode.Percent)
            {
                text += "%";
            }
        }

        return text;
    }
}
