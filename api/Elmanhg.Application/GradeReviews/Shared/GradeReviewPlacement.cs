namespace Elmanhg.Application.GradeReviews.Shared;

public sealed record GradeReviewPlacement(string UnitName, string LessonName)
{
    public static GradeReviewPlacement Unknown { get; } = new(string.Empty, string.Empty);
}
