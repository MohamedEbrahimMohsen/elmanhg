using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Exams.GetMultiUnitExamOverview;

public sealed class GetMultiUnitExamOverviewHandler(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionRepository questionRepository, ISessionRepository sessionRepository, ICurrentUserService currentUserService) : IRequestHandler<GetMultiUnitExamOverviewQuery, MultiUnitExamOverviewResult>
{
    public async Task<MultiUnitExamOverviewResult> Handle(GetMultiUnitExamOverviewQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var subject = await subjectRepository.GetByIdAsync(request.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (subject is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SubjectNotFound);
        }

        var units = await unitRepository.FindAsync(x => x.SubjectId == subject.Id, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var orderedUnits = units
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .ToList();
        var blueprints = await examBlueprintRepository.FindAsync(x => x.SubjectId == subject.Id, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var counts = await questionRepository.CountServableByUnitAndTypeAsync(subject.Id, cancellationToken).ConfigureAwait(false);
        var openExam = await sessionRepository.FirstOrDefaultAsync(x => x.StudentId == userId && x.Kind != SessionKind.Quiz && x.SubmittedAt == null, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return MultiUnitExamOverviewResultGenerator.Generate(subject, orderedUnits, blueprints, counts, openExam);
    }
}
