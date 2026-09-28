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

        if (questions.Count == 0)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SessionNoServableQuestions);
        }

        if (questions.Any(x => !IsServableInUnit(x, unit, lessons)))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SessionQuestionNotServable);
        }

        if (questions.Select(x => x.Id).Distinct().Count() != questions.Count)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SessionQuestionDuplicate);
        }

        var scope = new UnitExamScope(unit.Id);
        var started = ToMicroseconds(now);
        var session = new Session(Guid.NewGuid(), studentId)
        {
            StudentId = studentId,
            Kind = SessionKind.UnitExam,
            Scope = scope.ToJson(),
            ScopeKey = scope.ToKey(),
            IsTestMode = isTestMode,
            StartedAt = started,
            LastActivityAt = started,
            TimeLimitMinutes = blueprint.TimeLimitMinutes,
            PassMark = blueprint.PassMark,
            Deadline = blueprint.TimeLimitMinutes is null ? null : started.AddMinutes(blueprint.TimeLimitMinutes.Value),
        };
        session.Items.AddRange(questions.Select((question, index) => SessionItem.Create(session.Id, index + 1, question)));
        return session;
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

    private static bool IsServableInUnit(Question question, CurriculumUnit unit, IReadOnlyCollection<Lesson> lessons)
    {
        var lesson = lessons.FirstOrDefault(x => x.Id == question.LessonId);
        return lesson is not null && lesson.UnitId == unit.Id && ServableQuestionSpecification.IsSatisfiedBy(question, lesson);
    }
}
