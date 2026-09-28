using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Exams.PreviewMultiUnitExam;

public sealed class PreviewMultiUnitExamHandler(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionRepository questionRepository, IOptions<ExamBlueprintsOptions> examBlueprintsOptions, ICurrentUserService currentUserService) : IRequestHandler<PreviewMultiUnitExamQuery, MultiUnitExamPreviewResult>
{
    public async Task<MultiUnitExamPreviewResult> Handle(PreviewMultiUnitExamQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var input = request.Selection;
        var selection = await MultiUnitExamPlanner.LoadUnitsAsync(input.SubjectId, input.UnitIds ?? [], subjectRepository, unitRepository, cancellationToken).ConfigureAwait(false);
        var plan = await MultiUnitExamPlanner.PlanAsync(selection, input.Size, examBlueprintRepository, examBlueprintsOptions.Value.MaxTimeLimitMinutes, cancellationToken).ConfigureAwait(false);
        var unitIds = selection.Units
            .Select(x => x.Id)
            .ToHashSet();
        var counts = await questionRepository.CountServableByUnitAndTypeAsync(input.SubjectId, cancellationToken).ConfigureAwait(false);
        var available = counts
            .Where(x => unitIds.Contains(x.UnitId))
            .GroupBy(x => x.Type)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Count));
        return MultiUnitExamPreviewResultGenerator.Generate(selection, plan, input.Size, available);
    }
}
