using Elmanhg.Application.QuestionValidation.Shared;
using MediatR;

namespace Elmanhg.Application.QuestionValidation.GetValidationQuestion;

public sealed record GetValidationQuestionQuery(Guid QuestionId) : IRequest<ValidationQuestionDetailResult>;
