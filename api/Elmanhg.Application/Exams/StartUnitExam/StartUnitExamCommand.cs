using Elmanhg.Application.Exams.Shared;
using MediatR;

namespace Elmanhg.Application.Exams.StartUnitExam;

public sealed record StartUnitExamCommand(Guid UnitId) : IRequest<ExamSessionResult>;
