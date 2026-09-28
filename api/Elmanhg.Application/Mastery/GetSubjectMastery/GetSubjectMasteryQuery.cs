using Elmanhg.Application.Mastery.Shared;
using MediatR;

namespace Elmanhg.Application.Mastery.GetSubjectMastery;

public sealed record GetSubjectMasteryQuery(Guid SubjectId) : IRequest<SubjectMasteryDetailResult>;
