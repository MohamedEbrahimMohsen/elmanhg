using Elmanhg.Application.Shared.Authorization;
using Elmanhg.Application.Subjects.Shared;
using MediatR;

namespace Elmanhg.Application.Subjects.GetSubject;

public sealed record GetSubjectQuery(Guid SubjectId) : IRequest<SubjectDetailResult>, ISubjectScopedRequest;
