using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Sessions;

public partial class Session : AuditEntity, IVersioned
{
    public Guid StudentId { get; private set; }
    public SessionKind Kind { get; private set; }
    public string Scope { get; private set; } = "{}";
    public string ScopeKey { get; private set; } = string.Empty;
    public bool IsTestMode { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public decimal? ScorePercent { get; private set; }
    public int? TimeLimitMinutes { get; private set; }
    public int? PassMark { get; private set; }
    public DateTimeOffset? Deadline { get; private set; }
    public uint Version { get; private set; }
    public List<SessionItem> Items { get; private set; } = [];
    public List<Attempt> Attempts { get; private set; } = [];

    public bool IsSubmitted => SubmittedAt is not null;
    public bool IsExam => Kind != SessionKind.Quiz;
    public long TotalTimeTakenMilliseconds => Attempts.Sum(x => (long)x.TimeTakenMilliseconds);
    public int? CurrentPosition => IsSubmitted ? null : Items.OrderBy(x => x.Position).FirstOrDefault(x => FindAttempt(x.QuestionId) is null && FindPendingEssayAnswer(x) is null)?.Position;

    private Session(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Session StartQuiz(Guid studentId, Lesson lesson, IReadOnlyList<Question> questions, bool isTestMode)
    {
        if (questions.Count == 0)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SessionNoServableQuestions);
        }

        if (questions.Any(x => !ServableQuestionSpecification.IsSatisfiedBy(x, lesson)))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SessionQuestionNotServable);
        }

        if (questions.Select(x => x.Id).Distinct().Count() != questions.Count)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SessionQuestionDuplicate);
        }

        var scope = new QuizScope(lesson.Id);
        var now = UtcNowToMicroseconds();
        var session = new Session(Guid.NewGuid(), studentId)
        {
            StudentId = studentId,
            Kind = SessionKind.Quiz,
            Scope = scope.ToJson(),
            ScopeKey = scope.ToKey(),
            IsTestMode = isTestMode,
            StartedAt = now,
            LastActivityAt = now,
        };
        session.Items.AddRange(questions.Select((question, index) => SessionItem.Create(session.Id, index + 1, question)));
        return session;
    }

    public void Resume()
    {
        EnsureNotSubmitted();
        Touch(UtcNowToMicroseconds());
    }

    // timestamptz stores whole microseconds; truncating here keeps the first response identical to every later read and replay.
    private static DateTimeOffset ToMicroseconds(DateTimeOffset value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));

    private static DateTimeOffset UtcNowToMicroseconds() => ToMicroseconds(DateTimeOffset.UtcNow);

    private void Touch(DateTimeOffset now)
    {
        LastActivityAt = now;
        UpdatedBy = StudentId;
        UpdationDate = now;
    }

    private void EnsureNotSubmitted()
    {
        if (IsSubmitted)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SessionAlreadySubmitted);
        }
    }
}
