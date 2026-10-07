using Core.EntityFrameworkCore.Conflicts;

namespace Elmanhg.Tests.Core.Persistence;

public static class ConflictMapProbe
{
    public const string AlphaStale = "ALPHA_STALE", BetaStale = "BETA_STALE", ProbeTaken = "PROBE_TAKEN", ProbesTableTaken = "PROBES_TABLE_TAKEN";

    public static readonly UniqueViolation ProbeViolation = new("IX_Probe", "Probes");
}
