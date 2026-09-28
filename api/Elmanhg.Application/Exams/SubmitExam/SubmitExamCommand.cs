using Elmanhg.Application.Exams.Shared;
using MediatR;

namespace Elmanhg.Application.Exams.SubmitExam;

public sealed record SubmitExamCommand(Guid SessionId) : IRequest<ExamSessionResult>;
