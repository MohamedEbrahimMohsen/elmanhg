using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class ContentOptions
{
    public const string SectionName = "Content";

    [Range(1, int.MaxValue)]
    public int SubjectNameMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int UnitNameMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int LessonNameMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int LessonExplanationMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int LessonSummaryMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int LessonObjectiveMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int LessonObjectivesMaxCount { get; set; }

    [Range(1, int.MaxValue)]
    public int LessonVideoUrlMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int LessonImageMaxSizeInMb { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionStemMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionExplanationMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionOptionsMaxCount { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionOptionTextMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionBlanksMaxCount { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionAcceptedAnswersMaxCount { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionAnswerMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionTagsMaxCount { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionTagMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionMaxScoreMax { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionListMaxPageSize { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionFilterMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionImportMaxRows { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionImportMaxFileSizeInMb { get; set; }

    [Range(1, int.MaxValue)]
    public int QuestionEssayMaxWordsMax { get; set; } = 2000;

    [Range(1, int.MaxValue)]
    public int QuestionRubricCriteriaMaxCount { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int QuestionRubricLevelsMaxCount { get; set; } = 6;

    [Range(1, int.MaxValue)]
    public int QuestionRubricPointsMax { get; set; } = 100;

    [Range(1, int.MaxValue)]
    public int QuestionRubricTextMaxLength { get; set; } = 1000;

    [Range(1, int.MaxValue)]
    public int QuestionModelAnswersMaxCount { get; set; } = 3;

    [Range(1, int.MaxValue)]
    public int QuestionModelAnswerMaxLength { get; set; } = 20000;

    [Range(1, int.MaxValue)]
    public int QuestionEssayAnswerMaxLength { get; set; } = 20000;

    [Range(1, 3600)]
    public int ServableCountCacheSeconds { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int QuestionModelSolutionStepsMaxCount { get; set; } = 20;

    [Range(1, int.MaxValue)]
    public int QuestionModelSolutionStepMaxLength { get; set; } = 500;
}
