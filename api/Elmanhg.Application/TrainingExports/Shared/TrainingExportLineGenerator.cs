using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Domain.TrainingData;

namespace Elmanhg.Application.TrainingExports.Shared;

public static class TrainingExportLineGenerator
{
    public const string AvatarConversationScope = "avatar-conversation";
    public const string TeacherThreadScope = "teacher-thread";

    public static AttemptExportLine Attempt(AttemptTrainingRecord record)
    {
        return new AttemptExportLine(record.Id, record.StudentHash, record.QuestionId, record.QuestionVersion, record.SubjectId, record.UnitId, record.LessonId, record.SessionKind, TrainingDataScrubber.ScrubJson(record.Answer), record.Score, record.NormalisedScore, record.GradedBy, TrainingDataScrubber.ScrubJson(record.Grade), record.TimeTakenMilliseconds, record.OccurredAt);
    }

    public static AvatarExportLine Avatar(AvatarTrainingRecord record, IStudentIdHasher studentIdHasher)
    {
        return new AvatarExportLine(record.Id, record.StudentHash, studentIdHasher.HashSourceId(AvatarConversationScope, record.ConversationId), record.StudentMessagePosition, record.EntryPoint, record.SubjectId, record.UnitId, record.LessonId, record.QuestionId, TrainingDataScrubber.ScrubText(record.StudentText), TrainingDataScrubber.ScrubText(record.AssistantText), record.Model, record.PromptVersion, TrainingDataScrubber.ScrubJson(record.Context), record.AskedAt, record.OccurredAt);
    }

    public static TeacherThreadExportLine TeacherThread(TeacherThreadTrainingRecord record, IStudentIdHasher studentIdHasher)
    {
        return new TeacherThreadExportLine(record.Id, record.StudentHash, studentIdHasher.HashSourceId(TeacherThreadScope, record.ThreadId), record.Trigger, record.SubjectId, record.UnitId, record.LessonId, record.QuestionId, record.QuestionVersion, TrainingDataScrubber.ScrubJson(record.Context), TrainingDataScrubber.ScrubJson(record.Messages), record.Rating, record.SubmittedAt, record.OccurredAt);
    }

    public static EssayGradeExportLine EssayGrade(EssayGradeTrainingRecord record)
    {
        return new EssayGradeExportLine(record.Id, record.StudentHash, record.QuestionId, record.QuestionVersion, record.SubjectId, record.UnitId, record.LessonId, record.SessionKind, TrainingDataScrubber.ScrubJson(record.Answer), record.MaxScore, record.Score, record.NormalisedScore, TrainingDataScrubber.ScrubJson(record.Criteria), TrainingDataScrubber.ScrubText(record.Justification), record.Confidence, record.Outcome, record.Model, record.PromptVersion, record.OccurredAt, record.Trigger, record.ReviewDecision, record.ReviewedScore, record.ReviewedNormalisedScore, record.ReviewComment is null ? null : TrainingDataScrubber.ScrubText(record.ReviewComment), record.ReviewedAt);
    }
}
