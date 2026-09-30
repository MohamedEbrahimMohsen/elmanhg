using Elmanhg.Domain.TrainingExports;

namespace Elmanhg.Api.Controllers.TrainingExports;

public sealed record RequestTrainingExportRequest(TrainingExportSource Source, DateTimeOffset From, DateTimeOffset To, Guid? SubjectId);
