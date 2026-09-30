using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class GradeReviewOptions
{
    public const string SectionName = "GradeReview";

    [Range(1, 10000)]
    public int CommentMaxLength { get; set; } = 2000;

    [Range(1, 100)]
    public int QueueMaxPageSize { get; set; } = 50;
}
