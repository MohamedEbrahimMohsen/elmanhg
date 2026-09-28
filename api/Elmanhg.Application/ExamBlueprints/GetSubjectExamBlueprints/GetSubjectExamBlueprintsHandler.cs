using Core.Errors;
using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.ExamBlueprints.GetSubjectExamBlueprints;

public sealed class GetSubjectExamBlueprintsHandler(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionRepository questionRepository) : IRequestHandler<GetSubjectExamBlueprintsQuery, SubjectExamBlueprintsResult>
{
    public async Task<SubjectExamBlueprintsResult> Handle(GetSubjectExamBlueprintsQuery request, CancellationToken cancellationToken)
    {
        var subject = await subjectRepository.GetByIdAsync(request.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (subject is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SubjectNotFound);
        }

        var units = await unitRepository.FindAsync(x => x.SubjectId == subject.Id, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false);
        var blueprints = await examBlueprintRepository.FindAsync(x => x.SubjectId == subject.Id, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var counts = await questionRepository.CountServableByUnitAndTypeAsync(subject.Id, cancellationToken).ConfigureAwait(false);
        return ExamBlueprintResultGenerator.GenerateOverview(subject, units, blueprints, counts);
    }
}
