namespace Elmanhg.Application.Dashboard.GetValidationMetrics;

public sealed record TeacherThroughputResult(Guid TeacherId, string DisplayName, int Approved, int Rejected);
