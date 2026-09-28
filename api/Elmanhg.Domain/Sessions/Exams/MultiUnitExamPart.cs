using Elmanhg.Domain.ExamBlueprints;

namespace Elmanhg.Domain.Sessions.Exams;

public sealed record MultiUnitExamPart(Guid UnitId, ExamBlueprint Blueprint);
