using MediatR;

namespace Elmanhg.Application.Questions.GetServableQuestionCount;

public sealed record GetServableQuestionCountQuery : IRequest<ServableQuestionCountResult>;
