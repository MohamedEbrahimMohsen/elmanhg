using Elmanhg.Application.Subjects.Shared;
using MediatR;

namespace Elmanhg.Application.Subjects.GetSubjects;

public sealed record GetSubjectsQuery : IRequest<List<SubjectResult>>;
