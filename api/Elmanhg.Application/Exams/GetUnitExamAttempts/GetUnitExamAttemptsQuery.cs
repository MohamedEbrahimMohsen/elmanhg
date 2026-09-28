using Elmanhg.Application.Exams.Shared;
using MediatR;

namespace Elmanhg.Application.Exams.GetUnitExamAttempts;

public sealed record GetUnitExamAttemptsQuery(Guid UnitId) : IRequest<ExamAttemptsResult>;
