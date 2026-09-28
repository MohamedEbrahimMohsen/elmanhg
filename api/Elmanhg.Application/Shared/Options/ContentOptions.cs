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

    [Range(1, 3600)]
    public int ServableCountCacheSeconds { get; set; } = 60;
}
