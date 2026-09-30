namespace Elmanhg.Domain.TrainingData;

public sealed record TrainingRecordFilter(DateTimeOffset From, DateTimeOffset To, Guid? SubjectId);
