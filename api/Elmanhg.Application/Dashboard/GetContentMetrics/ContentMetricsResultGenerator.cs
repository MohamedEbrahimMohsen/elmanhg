using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.Dashboard.GetContentMetrics;

public static class ContentMetricsResultGenerator
{
    public static ContentMetricsResult Generate(Guid? subjectId, int subjects, int units, List<LessonStateCount> lessons, List<QuestionInventoryCount> inventory, int servable, DateTimeOffset generatedAt)
    {
        var byType = Enum.GetValues<QuestionType>()
            .Select(type => new QuestionTypeCountResult(type, inventory.Where(x => x.Type == type).Sum(x => x.Count)))
            .ToList();

        return new ContentMetricsResult(subjectId, subjects, units, LessonCount(lessons, LessonState.Draft), LessonCount(lessons, LessonState.Published), LessonCount(lessons, LessonState.Archived), QuestionCount(inventory, QuestionValidationStatus.Pending), QuestionCount(inventory, QuestionValidationStatus.Approved), QuestionCount(inventory, QuestionValidationStatus.Rejected), inventory.Where(x => x.IsRetired).Sum(x => x.Count), byType, servable, generatedAt);
    }

    private static int LessonCount(List<LessonStateCount> lessons, LessonState state) => lessons.Where(x => x.State == state).Sum(x => x.Count);

    private static int QuestionCount(List<QuestionInventoryCount> inventory, QuestionValidationStatus status) => inventory.Where(x => x.Status == status && !x.IsRetired).Sum(x => x.Count);
}
