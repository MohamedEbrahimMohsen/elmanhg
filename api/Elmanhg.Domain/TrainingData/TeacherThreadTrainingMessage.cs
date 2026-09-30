using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Domain.TrainingData;

public sealed record TeacherThreadTrainingMessage(TeacherThreadTrainingAuthor Author, TeacherMessageKind Kind, string Text, bool HasImage, DateTimeOffset SentAt);
