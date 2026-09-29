namespace Elmanhg.Application.Students.Shared;

public sealed record SubjectInterestsResult(bool NeedsOnboarding, List<SubjectInterestResult> Subjects);
