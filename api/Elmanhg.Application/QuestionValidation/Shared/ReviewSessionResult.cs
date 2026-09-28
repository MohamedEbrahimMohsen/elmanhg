namespace Elmanhg.Application.QuestionValidation.Shared;

public sealed record ReviewSessionResult(Guid ReviewSessionId, DateTimeOffset ExpiresAt);
