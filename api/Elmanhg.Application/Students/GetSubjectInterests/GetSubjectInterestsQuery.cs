using Elmanhg.Application.Students.Shared;
using MediatR;

namespace Elmanhg.Application.Students.GetSubjectInterests;

public sealed record GetSubjectInterestsQuery : IRequest<SubjectInterestsResult>;
