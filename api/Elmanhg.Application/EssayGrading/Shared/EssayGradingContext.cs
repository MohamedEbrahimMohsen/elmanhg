namespace Elmanhg.Application.EssayGrading.Shared;

public sealed record EssayGradingContext(string SubjectName, IReadOnlyList<string> Objectives);
