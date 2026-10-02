using MediatR;

namespace Elmanhg.Application.TeacherThreads.RescheduleTeacherThreadSlas;

public sealed record RescheduleTeacherThreadSlasCommand(int BatchSize) : IRequest<int>;
