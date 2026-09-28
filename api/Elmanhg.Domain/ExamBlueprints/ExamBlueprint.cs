using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Domain.ExamBlueprints;

public class ExamBlueprint : AuditEntity, IAuditedEntity
{
    public Guid SubjectId { get; private set; }
    public Guid? UnitId { get; private set; }
    public string TypeCounts { get; private set; } = "[]";
    public string? DifficultyMix { get; private set; }
    public int QuestionCount { get; private set; }
    public int? TimeLimitMinutes { get; private set; }
    public int PassMark { get; private set; }

    public bool IsSubjectDefault => UnitId is null;

    private ExamBlueprint(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static ExamBlueprint CreateForSubject(Subject subject, ExamBlueprintShape shape, IReadOnlyDictionary<QuestionType, int> servable, Guid createdBy)
    {
        EnsureNoShortfall(shape, servable);
        var blueprint = new ExamBlueprint(Guid.NewGuid(), createdBy) { SubjectId = subject.Id, UnitId = null };
        blueprint.Apply(shape);
        return blueprint;
    }

    public static ExamBlueprint CreateForUnit(CurriculumUnit unit, ExamBlueprintShape shape, IReadOnlyDictionary<QuestionType, int> servable, Guid createdBy)
    {
        EnsureNoShortfall(shape, servable);
        var blueprint = new ExamBlueprint(Guid.NewGuid(), createdBy) { SubjectId = unit.SubjectId, UnitId = unit.Id };
        blueprint.Apply(shape);
        return blueprint;
    }

    public List<ExamTypeCount> GetTypeCounts() => ExamBlueprintJson.DeserializeTypeCounts(TypeCounts);

    public ExamDifficultyMix? GetDifficultyMix() => DifficultyMix is null ? null : ExamBlueprintJson.DeserializeDifficultyMix(DifficultyMix);

    public void EnsureServable(IReadOnlyDictionary<QuestionType, int> servable)
    {
        var shortfalls = ExamBlueprintShortfall.Find(GetTypeCounts(), servable);
        if (shortfalls.Count > 0)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ExamShortfall, context: new Dictionary<string, object> { ["types"] = Describe(shortfalls) });
        }
    }

    public void Update(ExamBlueprintShape shape, IReadOnlyDictionary<QuestionType, int> servable, Guid updatedBy)
    {
        EnsureNoShortfall(shape, servable);
        Apply(shape);
        UpdatedBy = updatedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Delete(Guid deletedBy)
    {
        if (IsSubjectDefault)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ExamBlueprintDefaultNotDeletable);
        }

        SoftDelete();
        UpdatedBy = deletedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    private void Apply(ExamBlueprintShape shape)
    {
        TypeCounts = ExamBlueprintJson.SerializeTypeCounts(shape.TypeCounts);
        QuestionCount = shape.TypeCounts
            .Where(x => x.Count > 0)
            .Sum(x => x.Count);
        DifficultyMix = shape.DifficultyMix is null ? null : ExamBlueprintJson.SerializeDifficultyMix(shape.DifficultyMix);
        TimeLimitMinutes = shape.TimeLimitMinutes;
        PassMark = shape.PassMark;
    }

    private static void EnsureNoShortfall(ExamBlueprintShape shape, IReadOnlyDictionary<QuestionType, int> servable)
    {
        var shortfalls = ExamBlueprintShortfall.Find(shape.TypeCounts, servable);
        if (shortfalls.Count > 0)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ExamBlueprintShortfall, context: new Dictionary<string, object> { ["types"] = Describe(shortfalls) });
        }
    }

    private static string Describe(IEnumerable<ExamTypeShortfall> shortfalls) => string.Join(", ", shortfalls.Select(x => $"{x.Type} {x.Available}/{x.Required}"));
}
