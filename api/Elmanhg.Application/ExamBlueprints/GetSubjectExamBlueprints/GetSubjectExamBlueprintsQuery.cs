using Elmanhg.Application.ExamBlueprints.Shared;
using MediatR;

namespace Elmanhg.Application.ExamBlueprints.GetSubjectExamBlueprints;

public sealed record GetSubjectExamBlueprintsQuery(Guid SubjectId) : IRequest<SubjectExamBlueprintsResult>;
