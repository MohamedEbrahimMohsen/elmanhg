using MediatR;

namespace Elmanhg.Application.TeacherThreads.CanViewTeacherThreadImage;

public sealed record CanViewTeacherThreadImageQuery(string ImageUrl) : IRequest<bool>;
