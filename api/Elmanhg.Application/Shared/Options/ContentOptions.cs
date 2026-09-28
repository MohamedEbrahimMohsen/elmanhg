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
}
