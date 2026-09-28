using MediatR;

namespace Elmanhg.Application.QuestionValidation.RecordQuestionOpening;

public sealed record RecordQuestionOpeningCommand(Guid ReviewSessionId, Guid QuestionId) : IRequest;
