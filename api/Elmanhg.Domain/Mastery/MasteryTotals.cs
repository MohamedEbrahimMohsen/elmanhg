namespace Elmanhg.Domain.Mastery;

public sealed record MasteryTotals(int ServableCount, int MasteredCount, int SeenCount)
{
    public int RemainingCount => ServableCount - MasteredCount;

    public int MasteryPercent => ServableCount == 0 ? 0 : MasteredCount * 100 / ServableCount;

    public static MasteryTotals Of(IEnumerable<LessonMasteryCount> lessons)
    {
        var list = lessons.ToList();
        return new(list.Sum(x => x.ServableCount), list.Sum(x => x.MasteredCount), list.Sum(x => x.SeenCount));
    }
}
