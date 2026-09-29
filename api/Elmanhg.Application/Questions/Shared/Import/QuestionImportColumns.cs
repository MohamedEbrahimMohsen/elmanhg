using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using System.Globalization;
using ToleranceModeKind = Elmanhg.Domain.Questions.Schemas.ToleranceMode;

namespace Elmanhg.Application.Questions.Shared.Import;

public static class QuestionImportColumns
{
    public const string Stem = "stem";
    public const string Explanation = "explanation";
    public const string Difficulty = "difficulty";
    public const string MaxScore = "max_score";
    public const string Objective = "objective";
    public const string Tags = "tags";
    public const string Correct = "correct";
    public const string PartialCredit = "partial_credit";
    public const string CorrectAnswer = "correct_answer";
    public const string UnifyLetterVariants = "unify_letter_variants";
    public const string AnswerKind = "answer_kind";
    public const string Value = "value";
    public const string Tolerance = "tolerance";
    public const string ToleranceMode = "tolerance_mode";
    public const string AcceptedAnswers = "accepted_answers";

    // Option ids are single lower-case letters, the ids the question editor generates.
    private const int MaxLetterOptions = 26;

    private static readonly IReadOnlyList<string> CommonTail = [Explanation, Difficulty, MaxScore, Objective, Tags];

    public static IReadOnlyList<QuestionType> Types { get; } = [QuestionType.Mcq, QuestionType.Multi, QuestionType.TrueFalse, QuestionType.Fill, QuestionType.Short];

    public static string Option(string id) => $"option_{id}";

    public static string Blank(string id) => $"blank_{id}";

    public static IReadOnlyList<string> OptionIds(ContentOptions options)
    {
        return Enumerable.Range(0, Math.Min(options.QuestionOptionsMaxCount, MaxLetterOptions))
            .Select(x => ((char)('a' + x)).ToString())
            .ToList();
    }

    public static IReadOnlyList<string> BlankIds(ContentOptions options)
    {
        return Enumerable.Range(1, options.QuestionBlanksMaxCount)
            .Select(x => x.ToString(CultureInfo.InvariantCulture))
            .ToList();
    }

    public static IReadOnlyList<string> For(QuestionType type, ContentOptions options)
    {
        IReadOnlyList<string> head = type switch
        {
            QuestionType.Mcq => [Stem, .. OptionIds(options).Select(Option), Correct],
            QuestionType.Multi => [Stem, .. OptionIds(options).Select(Option), Correct, PartialCredit],
            QuestionType.TrueFalse => [Stem, CorrectAnswer],
            QuestionType.Fill => [Stem, .. BlankIds(options).Select(Blank), UnifyLetterVariants],
            QuestionType.Short => [Stem, AnswerKind, Value, Tolerance, ToleranceMode, AcceptedAnswers, UnifyLetterVariants],
            _ => throw new InvalidOperationException("Unsupported question type."),
        };
        return [.. head, .. CommonTail];
    }

    public static bool IsRequired(QuestionType type, string column)
    {
        return column switch
        {
            Stem or Difficulty => true,
            Correct => type is QuestionType.Mcq or QuestionType.Multi,
            CorrectAnswer => type is QuestionType.TrueFalse,
            AnswerKind => type is QuestionType.Short,
            _ => type switch
            {
                QuestionType.Mcq or QuestionType.Multi => column == Option("a") || column == Option("b"),
                QuestionType.Fill => column == Blank("1"),
                _ => false,
            },
        };
    }

    public static IReadOnlyList<string> AllowedValues(string column)
    {
        return column switch
        {
            Difficulty => Tokens<QuestionDifficulty>(),
            AnswerKind => Tokens<ShortAnswerKind>(),
            ToleranceMode => Tokens<ToleranceModeKind>(),
            PartialCredit or CorrectAnswer or UnifyLetterVariants => [bool.TrueString.ToLowerInvariant(), bool.FalseString.ToLowerInvariant()],
            _ => [],
        };
    }

    public static QuestionType? TypeForSheet(string sheetName)
    {
        var name = sheetName.Trim();
        return Types
            .Select(x => (QuestionType?)x)
            .FirstOrDefault(x => string.Equals(x.ToString(), name, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> Tokens<TEnum>() where TEnum : struct, Enum
    {
        return Enum.GetNames<TEnum>()
            .Select(x => x.ToLowerInvariant())
            .ToList();
    }
}
