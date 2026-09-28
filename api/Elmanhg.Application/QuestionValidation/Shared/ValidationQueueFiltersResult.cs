namespace Elmanhg.Application.QuestionValidation.Shared;

public sealed record ValidationSubjectOption(Guid Id, string Name);

public sealed record ValidationUnitOption(Guid Id, Guid SubjectId, string Name);

public sealed record ValidationLessonOption(Guid Id, Guid UnitId, string Name);

public sealed record ValidationQueueFiltersResult(List<ValidationSubjectOption> Subjects, List<ValidationUnitOption> Units, List<ValidationLessonOption> Lessons);
