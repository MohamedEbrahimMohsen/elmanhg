using Elmanhg.Application.Sessions.Shared;
using MediatR;

namespace Elmanhg.Application.Sessions.StartQuizSession;

public sealed record StartQuizSessionCommand(Guid LessonId, int? QuestionCount) : IRequest<SessionResult>;
