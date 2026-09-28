using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Tests.Builders;

public sealed class ExamBlueprintBuilder
{
    public ExamBlueprintBuilder()
    {
        Unit = CurriculumUnit.Create(Subject, "Mechanics", 1, Guid.NewGuid());
    }

    public Subject Subject { get; } = Subject.Create("Physics", 1, Guid.NewGuid());

    public CurriculumUnit Unit { get; }

    public Guid CreatedBy { get; } = Guid.NewGuid();

    public static ExamBlueprintShape Shape(params ExamTypeCount[] counts) => new(counts, null, 45, 50);

    public static Dictionary<QuestionType, int> Plenty() => Enum.GetValues<QuestionType>().ToDictionary(x => x, _ => 100);

    public ExamBlueprint BuildDefault() => ExamBlueprint.CreateForSubject(Subject, Shape(new ExamTypeCount(QuestionType.Mcq, 2)), Plenty(), CreatedBy);

    public ExamBlueprint BuildForUnit() => ExamBlueprint.CreateForUnit(Unit, Shape(new ExamTypeCount(QuestionType.Mcq, 2)), Plenty(), CreatedBy);
}
