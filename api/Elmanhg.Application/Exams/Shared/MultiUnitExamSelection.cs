namespace Elmanhg.Application.Exams.Shared;

public sealed record MultiUnitExamSelection(Guid SubjectId, List<Guid>? UnitIds, int Size);
