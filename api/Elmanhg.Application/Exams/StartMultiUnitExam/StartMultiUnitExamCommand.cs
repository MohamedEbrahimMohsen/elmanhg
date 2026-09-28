using Elmanhg.Application.Exams.Shared;
using MediatR;

namespace Elmanhg.Application.Exams.StartMultiUnitExam;

public sealed record StartMultiUnitExamCommand(MultiUnitExamSelection Selection) : IRequest<ExamSessionResult>;
