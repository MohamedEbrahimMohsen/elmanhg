using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;

namespace Elmanhg.Application.Mastery.Shared;

public static class MasteryOverviewResultGenerator
{
    public static MasteryOverviewResult Generate(IReadOnlyCollection<LessonMasteryCount> lessons, IReadOnlyList<Subject> subjects, int streakDays, LessonMasteryCount? next, Lesson? nextLesson)
    {
        var totals = MasteryTotals.Of(lessons);
        var headline = new MasteryHeadlineResult(totals.ServableCount, totals.MasteredCount, totals.RemainingCount, totals.SeenCount);
        var subjectResults = subjects
            .Select(subject => GenerateSubject(subject, MasteryTotals.Of(lessons.Where(x => x.SubjectId == subject.Id))))
            .ToList();
        return new MasteryOverviewResult(headline, streakDays, GenerateNextLesson(subjects, next, nextLesson), subjectResults);
    }

    private static SubjectMasteryResult GenerateSubject(Subject subject, MasteryTotals totals) => new(subject.Id, subject.Name, totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent);

    private static NextLessonResult? GenerateNextLesson(IReadOnlyList<Subject> subjects, LessonMasteryCount? next, Lesson? nextLesson)
    {
        var subject = next is null ? null : subjects.FirstOrDefault(x => x.Id == next.SubjectId);
        if (next is null || nextLesson is null || subject is null)
        {
            return null;
        }

        var percent = new MasteryTotals(next.ServableCount, next.MasteredCount, next.SeenCount).MasteryPercent;
        return new NextLessonResult(nextLesson.Id, nextLesson.Name, subject.Id, subject.Name, percent);
    }
}
