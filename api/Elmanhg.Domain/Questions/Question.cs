using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Units;

namespace Elmanhg.Domain.Questions;

public partial class Question : AuditEntity, IAuditedEntity
{
    public Guid LessonId { get; private set; }
    public Guid SubjectId { get; private set; }
    public QuestionType Type { get; private set; }
    public string Stem { get; private set; } = string.Empty;
    public string Body { get; private set; } = "{}";
    public string GradingSpec { get; private set; } = "{}";
    public string Explanation { get; private set; } = string.Empty;
    public QuestionDifficulty Difficulty { get; private set; }
    public Guid? ObjectiveId { get; private set; }
    public List<string> Tags { get; private set; } = [];
    public int MaxScore { get; private set; }
    public int Version { get; private set; }
    public QuestionValidationStatus ValidationStatus { get; private set; }
    public Guid? ValidatedBy { get; private set; }
    public DateTimeOffset? ValidatedAt { get; private set; }
    public List<QuestionRevision> Revisions { get; private set; } = [];

    public QuestionContent CurrentContent => new(Stem, Body, GradingSpec, Explanation, MaxScore);

    private Question(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Question Create(Lesson lesson, CurriculumUnit unit, QuestionType type, QuestionContent content, QuestionMetadata metadata, Guid createdBy)
    {
        EnsureObjectiveInLesson(lesson, metadata.ObjectiveId);

        var question = new Question(Guid.NewGuid(), createdBy)
        {
            LessonId = lesson.Id,
            SubjectId = unit.SubjectId,
            Type = type,
            Version = 1,
            ValidationStatus = QuestionValidationStatus.Pending,
        };
        question.ApplyContent(content);
        question.ApplyMetadata(metadata);
        question.Revisions.Add(QuestionRevision.Create(question, createdBy));
        return question;
    }

    // Assigns only what differs so EF change tracking and the audit diff never see a formatting-only change.
    private void ApplyContent(QuestionContent content)
    {
        if (Stem != content.Stem)
        {
            Stem = content.Stem;
        }

        if (!QuestionJson.AreEquivalent(Body, content.Body))
        {
            Body = content.Body;
        }

        if (!QuestionJson.AreEquivalent(GradingSpec, content.GradingSpec))
        {
            GradingSpec = content.GradingSpec;
        }

        if (Explanation != content.Explanation)
        {
            Explanation = content.Explanation;
        }

        if (MaxScore != content.MaxScore)
        {
            MaxScore = content.MaxScore;
        }
    }

    private void ApplyMetadata(QuestionMetadata metadata)
    {
        Difficulty = metadata.Difficulty;
        ObjectiveId = metadata.ObjectiveId;
        if (!Tags.SequenceEqual(metadata.Tags))
        {
            Tags = metadata.Tags.ToList();
        }
    }

    private static void EnsureObjectiveInLesson(Lesson lesson, Guid? objectiveId)
    {
        if (objectiveId is not null && lesson.Objectives.All(x => x.Id != objectiveId))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionObjectiveNotInLesson);
        }
    }
}
