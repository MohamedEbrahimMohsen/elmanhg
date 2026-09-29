using Elmanhg.Domain.Sessions;

namespace Elmanhg.Application.Browse.Shared;

public static class UnitBestScore
{
    public static decimal? Find(IReadOnlyCollection<ExamBestScore> bests, Guid unitId) => bests.FirstOrDefault(x => x.ScopeKey == new UnitExamScope(unitId).ToKey())?.BestScorePercent;
}
