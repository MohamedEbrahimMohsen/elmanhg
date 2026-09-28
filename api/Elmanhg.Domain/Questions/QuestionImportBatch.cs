using Core.DDD.Entities;

namespace Elmanhg.Domain.Questions;

public class QuestionImportBatch : AuditEntity, IAuditedEntity
{
    public Guid LessonId { get; private set; }
    public string FileHash { get; private set; } = string.Empty;
    public int QuestionCount { get; private set; }

    private QuestionImportBatch(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static QuestionImportBatch Create(Guid batchId, Guid lessonId, string fileHash, int questionCount, Guid createdBy)
    {
        return new QuestionImportBatch(batchId, createdBy)
        {
            LessonId = lessonId,
            FileHash = fileHash,
            QuestionCount = questionCount,
        };
    }

    public bool Matches(Guid lessonId, string fileHash) => LessonId == lessonId && FileHash == fileHash;
}
