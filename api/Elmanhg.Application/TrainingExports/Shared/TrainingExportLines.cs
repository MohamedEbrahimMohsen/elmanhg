using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.TrainingData;
using System.Text.Json.Nodes;

namespace Elmanhg.Application.TrainingExports.Shared;

public sealed record AttemptExportLine(Guid RecordId, string StudentHash, Guid QuestionId, int QuestionVersion, Guid SubjectId, Guid UnitId, Guid LessonId, SessionKind SessionKind, JsonNode? Answer, decimal Score, decimal NormalisedScore, AttemptGrader GradedBy, JsonNode? Grade, int TimeTakenMilliseconds, DateTimeOffset OccurredAt);

public sealed record AvatarExportLine(Guid RecordId, string StudentHash, string ConversationKey, int Position, AvatarEntryPoint EntryPoint, Guid? SubjectId, Guid? UnitId, Guid? LessonId, Guid? QuestionId, string StudentText, string AssistantText, string Model, string PromptVersion, JsonNode? Context, DateTimeOffset AskedAt, DateTimeOffset OccurredAt);

public sealed record TeacherThreadExportLine(Guid RecordId, string StudentHash, string ThreadKey, TeacherThreadTrainingTrigger Trigger, Guid SubjectId, Guid UnitId, Guid LessonId, Guid? QuestionId, int? QuestionVersion, JsonNode? Context, JsonNode? Messages, int? Rating, DateTimeOffset SubmittedAt, DateTimeOffset OccurredAt);

public sealed record EssayGradeExportLine(Guid RecordId, string StudentHash, Guid QuestionId, int QuestionVersion, Guid SubjectId, Guid UnitId, Guid LessonId, SessionKind SessionKind, JsonNode? Answer, int MaxScore, decimal Score, decimal NormalisedScore, JsonNode? Criteria, string Justification, decimal Confidence, EssayGradeStatus Outcome, string Model, string PromptVersion, DateTimeOffset OccurredAt);
