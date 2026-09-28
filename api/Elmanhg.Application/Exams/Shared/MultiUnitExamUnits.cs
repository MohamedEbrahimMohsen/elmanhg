using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Exams.Shared;

public sealed record MultiUnitExamUnits(Subject Subject, List<CurriculumUnit> Units);
