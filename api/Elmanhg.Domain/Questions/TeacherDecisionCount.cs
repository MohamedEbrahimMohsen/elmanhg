namespace Elmanhg.Domain.Questions;

public sealed record TeacherDecisionCount(Guid TeacherId, int Approved, int Rejected);
