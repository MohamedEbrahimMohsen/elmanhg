using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using static Elmanhg.Application.Questions.Shared.Import.QuestionImportBodies;
using static Elmanhg.Application.Questions.Shared.Import.QuestionImportColumns;

namespace Elmanhg.Application.Questions.Shared.Import;

public static class QuestionImportRowMapper
{
    // Matches the question editor's default points.
    private const int DefaultMaxScore = 1;

    public static QuestionFields Map(QuestionType type, QuestionImportCells cells, Lesson lesson, ContentOptions options)
    {
        var (body, spec) = type switch
        {
            QuestionType.Mcq => ReadChoice(cells, false, options),
            QuestionType.Multi => ReadChoice(cells, true, options),
            QuestionType.TrueFalse => ReadTrueFalse(cells),
            QuestionType.Fill => ReadFill(cells, options),
            QuestionType.Short => ReadShort(cells),
            _ => throw new InvalidOperationException("Unsupported question type."),
        };
        var objectiveId = ResolveObjective(cells, lesson);
        return new QuestionFields(type, QuestionImportText.ToHtml(cells.Text(Stem)), body, spec, QuestionImportText.ToHtml(cells.Text(Explanation)), cells.Enum<QuestionDifficulty>(Difficulty), objectiveId, cells.List(Tags), cells.Integer(MaxScore) ?? DefaultMaxScore);
    }

    private static Guid? ResolveObjective(QuestionImportCells cells, Lesson lesson)
    {
        var number = cells.Integer(Objective);
        if (number is null)
        {
            return null;
        }

        var objectives = lesson.Objectives
            .OrderBy(x => x.Order)
            .ToList();
        if (number < 1 || number > objectives.Count)
        {
            cells.AddError(Objective, ErrorCodes.QuestionImportObjectiveInvalid);
            return null;
        }

        return objectives[number.Value - 1].Id;
    }
}
