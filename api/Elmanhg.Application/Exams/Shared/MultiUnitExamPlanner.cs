using Core.DDD.Repositories;
using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Exams.Shared;

public static class MultiUnitExamPlanner
{
    public static async Task<MultiUnitExamUnits> LoadUnitsAsync(Guid subjectId, IReadOnlyCollection<Guid> unitIds, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, CancellationToken cancellationToken)
    {
        var subject = await subjectRepository.GetRequiredAsync(subjectId, ErrorCodes.SubjectNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        var units = await unitRepository.FindAsync(x => x.SubjectId == subjectId && unitIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (units.Count != unitIds.Distinct().Count())
        {
            throw new NotFoundCoreException(ErrorCodes.UnitNotFound);
        }

        return new MultiUnitExamUnits(subject, units
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .ToList());
    }

    public static async Task<MultiUnitExamPlan> PlanAsync(MultiUnitExamUnits selection, int size, IExamBlueprintRepository examBlueprintRepository, int maxTimeLimitMinutes, CancellationToken cancellationToken)
    {
        var blueprints = await examBlueprintRepository.FindAsync(x => x.SubjectId == selection.Subject.Id, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var resolved = selection.Units
            .Select(unit => (Unit: unit, Blueprint: ExamBlueprintResolution.ForUnit(blueprints, unit)))
            .ToList();
        var missing = resolved
            .Where(x => x.Blueprint is null)
            .Select(x => x.Unit.Name)
            .ToList();
        if (missing.Count > 0)
        {
            throw new BadRequestCoreException(ErrorCodes.MultiUnitExamNoBlueprint, context: new Dictionary<string, object> { ["units"] = string.Join(", ", missing) });
        }

        var parts = resolved
            .Select(x => new MultiUnitExamPart(x.Unit.Id, x.Blueprint ?? throw new InvalidOperationException("Blueprint was resolved above.")))
            .ToList();
        return MultiUnitBlueprintMerge.Merge(parts, size, maxTimeLimitMinutes);
    }
}
