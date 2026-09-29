using MediatR;

namespace Elmanhg.Application.TeacherThreads.MarkTeacherThreadRead;

public sealed record MarkTeacherThreadReadCommand(Guid ThreadId) : IRequest;
