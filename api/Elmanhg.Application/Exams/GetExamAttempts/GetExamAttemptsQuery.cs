using Elmanhg.Application.Exams.Shared;
using MediatR;

namespace Elmanhg.Application.Exams.GetExamAttempts;

public sealed record GetExamAttemptsQuery(Guid SessionId) : IRequest<ExamAttemptsResult>;
