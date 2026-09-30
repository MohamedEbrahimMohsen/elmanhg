using Elmanhg.Application.Progress.Shared;
using MediatR;

namespace Elmanhg.Application.Progress.GetStudentProgress;

public sealed record GetStudentProgressQuery(Guid StudentId) : IRequest<StudentProgressResult>;
