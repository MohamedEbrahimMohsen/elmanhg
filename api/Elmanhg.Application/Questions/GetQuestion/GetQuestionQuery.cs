using Elmanhg.Application.Questions.Shared;
using MediatR;

namespace Elmanhg.Application.Questions.GetQuestion;

public sealed record GetQuestionQuery(Guid QuestionId) : IRequest<QuestionDetailResult>;
