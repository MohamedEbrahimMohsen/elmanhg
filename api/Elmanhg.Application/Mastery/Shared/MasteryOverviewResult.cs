namespace Elmanhg.Application.Mastery.Shared;

public sealed record MasteryOverviewResult(MasteryHeadlineResult Headline, int StreakDays, NextLessonResult? NextLesson, List<SubjectMasteryResult> Subjects);
