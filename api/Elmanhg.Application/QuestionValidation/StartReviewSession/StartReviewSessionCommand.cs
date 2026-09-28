using Elmanhg.Application.QuestionValidation.Shared;
using MediatR;

namespace Elmanhg.Application.QuestionValidation.StartReviewSession;

public sealed record StartReviewSessionCommand : IRequest<ReviewSessionResult>;
