namespace Elmanhg.Domain.Mastery;

public static class StudyStreak
{
    public static int Count(IReadOnlyCollection<DateOnly> activeDays, DateOnly today)
    {
        var days = activeDays.ToHashSet();
        var cursor = days.Contains(today) ? today : today.AddDays(-1);
        var count = 0;
        while (days.Contains(cursor))
        {
            count++;
            cursor = cursor.AddDays(-1);
        }

        return count;
    }
}
