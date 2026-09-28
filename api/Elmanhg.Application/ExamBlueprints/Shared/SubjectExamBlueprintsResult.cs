namespace Elmanhg.Application.ExamBlueprints.Shared;

public sealed record SubjectExamBlueprintsResult(Guid SubjectId, string SubjectName, ExamBlueprintResult? DefaultBlueprint, List<ExamTypeCountResult> Servable, List<UnitExamBlueprintResult> Units);

public sealed record UnitExamBlueprintResult(Guid UnitId, string Name, int Order, ExamBlueprintResult? Blueprint, List<ExamTypeCountResult> Servable);
