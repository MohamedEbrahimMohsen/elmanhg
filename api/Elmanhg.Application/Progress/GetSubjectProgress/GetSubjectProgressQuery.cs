using Elmanhg.Application.Progress.Shared;
using MediatR;

namespace Elmanhg.Application.Progress.GetSubjectProgress;

public sealed record GetSubjectProgressQuery : IRequest<List<SubjectProgressResult>>;
