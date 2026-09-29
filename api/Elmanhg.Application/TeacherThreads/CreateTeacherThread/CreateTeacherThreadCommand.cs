using Elmanhg.Application.TeacherThreads.Shared;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Application.TeacherThreads.CreateTeacherThread;

public sealed record CreateTeacherThreadCommand(string? Text, Guid? LessonId, Guid? QuestionId, Guid? AttemptId, IFormFile? Image) : IRequest<TeacherThreadResult>;
