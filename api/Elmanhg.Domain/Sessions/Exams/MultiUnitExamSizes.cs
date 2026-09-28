namespace Elmanhg.Domain.Sessions.Exams;

public static class MultiUnitExamSizes
{
    public const int MinUnits = 2;

    // PRD §7.5 target sizes for a multi-unit exam.
    public static readonly IReadOnlyList<int> All = [20, 40, 60];

    public static bool IsAllowed(int size) => All.Contains(size);
}
