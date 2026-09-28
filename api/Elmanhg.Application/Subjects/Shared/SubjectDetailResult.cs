using Elmanhg.Application.Units.Shared;

namespace Elmanhg.Application.Subjects.Shared;

public sealed record SubjectDetailResult(Guid Id, string Name, int Order, List<UnitResult> Units);
