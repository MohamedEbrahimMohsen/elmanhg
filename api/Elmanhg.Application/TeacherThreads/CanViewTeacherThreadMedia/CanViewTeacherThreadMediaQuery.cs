using MediatR;

namespace Elmanhg.Application.TeacherThreads.CanViewTeacherThreadMedia;

public sealed record CanViewTeacherThreadMediaQuery(string MediaUrl) : IRequest<bool>;
