using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Units;

namespace Elmanhg.Domain.Lessons;

public partial class Lesson : AuditEntity, IAuditedEntity
{
    public Guid UnitId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public LessonState State { get; private set; }
    public string Explanation { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string? VideoUrl { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public List<LessonObjective> Objectives { get; private set; } = [];

    private Lesson(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Lesson Create(CurriculumUnit unit, string name, int order, Guid createdBy)
    {
        if (order < 1)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid);
        }

        return new Lesson(Guid.NewGuid(), createdBy)
        {
            UnitId = unit.Id,
            Name = name.Trim(),
            Order = order,
            State = LessonState.Draft,
        };
    }

    public void Update(string name, string explanation, string summary, string? videoUrl, IReadOnlyList<LessonObjectiveContent> objectives, Guid updatedBy)
    {
        var keptIds = objectives
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id.GetValueOrDefault())
            .ToHashSet();
        if (keptIds.Any(id => Objectives.All(x => x.Id != id)))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.LessonObjectiveUnknown);
        }

        foreach (var removed in Objectives.Where(x => !keptIds.Contains(x.Id)).ToList())
        {
            removed.Delete(updatedBy);
        }

        for (var index = 0; index < objectives.Count; index++)
        {
            var content = objectives[index];
            if (content.Id is null)
            {
                Objectives.Add(LessonObjective.Create(Id, content.Text, index + 1, updatedBy));
            }
            else
            {
                Objectives.Single(x => x.Id == content.Id).Update(content.Text, index + 1, updatedBy);
            }
        }

        Name = name.Trim();
        Explanation = explanation;
        Summary = summary;
        VideoUrl = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl.Trim();
        UpdatedBy = updatedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void MoveTo(int order, Guid updatedBy)
    {
        if (order < 1)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid);
        }

        if (Order == order)
        {
            return;
        }

        Order = order;
        UpdatedBy = updatedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Delete(bool hasQuestions, Guid deletedBy)
    {
        if (State == LessonState.Published)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.LessonIsPublished);
        }

        if (hasQuestions)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.LessonHasQuestions);
        }

        foreach (var objective in Objectives)
        {
            objective.Delete(deletedBy);
        }

        var now = DateTimeOffset.UtcNow;
        SoftDelete(now);
        UpdatedBy = deletedBy;
        UpdationDate = now;
    }
}
