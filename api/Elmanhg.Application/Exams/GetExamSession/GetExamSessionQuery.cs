using Elmanhg.Application.Exams.Shared;
using MediatR;

namespace Elmanhg.Application.Exams.GetExamSession;

public sealed record GetExamSessionQuery(Guid SessionId) : IRequest<ExamSessionResult>;
