using MediatR;

namespace Elmanhg.Application.Exams.AutoSubmitExam;

public sealed record AutoSubmitExamCommand(Guid SessionId) : IRequest;
