using Elmanhg.Domain.Questions;

namespace Elmanhg.Api.Controllers.QuestionValidation;

public sealed record ApproveQuestionRequest(int? Version, QuestionDifficulty? Difficulty);

public sealed record RejectQuestionRequest(int? Version, string? Reason);

public sealed record BulkApproveQuestionsRequest(Guid ReviewSessionId, List<Guid>? QuestionIds);
