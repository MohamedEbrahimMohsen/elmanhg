using Elmanhg.Application.TeacherThreads.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherThreads.GetTeacherReplyDeadline;

public sealed record GetTeacherReplyDeadlineQuery : IRequest<TeacherReplyDeadlineResult>;
