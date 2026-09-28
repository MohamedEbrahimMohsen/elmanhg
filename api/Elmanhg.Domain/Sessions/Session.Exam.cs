using Core.Errors;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Units;

namespace Elmanhg.Domain.Sessions;

public partial class Session
{
    public static Session StartUnitExam(Guid studentId, CurriculumUnit unit, ExamBlueprint blueprint, IReadOnlyList<Question> questions, IReadOnlyCollection<Lesson> lessons, bool isTestMode, DateTimeOffset now)
    {
        if (blueprint.UnitId != unit.Id && !(blueprint.IsSubjectDefault && blueprint.SubjectId == unit.SubjectId))
        {
            throw new InvalidOperationException("The blueprint does not apply to this unit.");
        }

        HashSet<Guid> unitIds = [unit.Id];
        EnsureExamQuestions(questions, x => IsServableInUnits(x, unitIds, lessons));
        var scope = new UnitExamScope(unit.Id);
        return CreateExam(studentId, SessionKind.UnitExam, scope.ToJson(), scope.ToKey(), blueprint.TimeLimitMinutes, blueprint.PassMark, questions, isTestMode, now);
    }

    public bool IsPastDeadline(DateTimeOffset now, TimeSpan grace) => Deadline is not null && now > Deadline.Value + grace;

    public void SaveExamAnswer(SessionItem item, string answer, TimeSpan grace, DateTimeOffset now)
    {
        if (!Items.Contains(item))
        {
            throw new InvalidOperationException("Session item does not belong to this session.");
        }

        if (!IsExam)
        {
            throw new InvalidOperationException("Quiz answers are recorded with RecordAttempt.");
        }

        EnsureNotSubmitted();
        var at = ToMicroseconds(now);
        if (IsPastDeadline(at, grace))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ExamTimeExpired);
        }

        item.SaveAnswer(answer, at);
        Touch(at);
    }

    private static void EnsureExamQuestions(IReadOnlyList<Question> questions, Func<Question, bool> isServable)
    {
        if (questions.Count == 0)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SessionNoServableQuestions);
        }

        if (questions.Any(x => !isServable(x)))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SessionQuestionNotServable);
        }

        if (questions.Select(x => x.Id).Distinct().Count() != questions.Count)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SessionQuestionDuplicate);
        }
    }

    private static Session CreateExam(Guid studentId, SessionKind kind, string scope, string scopeKey, int? timeLimitMinutes, int passMark, IReadOnlyList<Question> questions, bool isTestMode, DateTimeOffset now)
    {
        var started = ToMicroseconds(now);
        var session = new Session(Guid.NewGuid(), studentId)
        {
            StudentId = studentId,
            Kind = kind,
            Scope = scope,
            ScopeKey = scopeKey,
            IsTestMode = isTestMode,
            StartedAt = started,
            LastActivityAt = started,
            TimeLimitMinutes = timeLimitMinutes,
            PassMark = passMark,
            Deadline = timeLimitMinutes is null ? null : started.AddMinutes(timeLimitMinutes.Value),
        };
        session.Items.AddRange(questions.Select((question, index) => SessionItem.Create(session.Id, index + 1, question)));
        return session;
    }

    private static bool IsServableInUnits(Question question, IReadOnlySet<Guid> unitIds, IReadOnlyCollection<Lesson> lessons)
    {
        var lesson = lessons.FirstOrDefault(x => x.Id == question.LessonId);
        return lesson is not null && unitIds.Contains(lesson.UnitId) && ServableQuestionSpecification.IsSatisfiedBy(question, lesson);
    }
}
