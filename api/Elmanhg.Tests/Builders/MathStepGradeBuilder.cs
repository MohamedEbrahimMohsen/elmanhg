using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Tests.Builders;

public sealed class MathStepGradeBuilder
{
    public const string DefaultAnswer = """{"steps":["2x = 4"],"finalAnswer":"x = 2"}""";
    public static readonly DateTimeOffset DefaultRequestedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private Guid _studentId = Guid.NewGuid();
    private Guid _sessionId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private Guid _questionId = Guid.NewGuid();
    private int _questionVersion = 1;
    private int _maxScore = 2;
    private int _timeTakenMilliseconds;
    private string _answer = DefaultAnswer;
    private MathAnswerVerdict? _verdict = MathAnswerVerdict.Equivalent;
    private DateTimeOffset _requestedAt = DefaultRequestedAt;

    public MathStepGradeBuilder ForStudent(Guid studentId)
    {
        _studentId = studentId;
        return this;
    }

    public MathStepGradeBuilder ForSession(Guid sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public MathStepGradeBuilder ForQuestion(Guid questionId, int version)
    {
        _questionId = questionId;
        _questionVersion = version;
        return this;
    }

    public MathStepGradeBuilder WithVerdict(MathAnswerVerdict? verdict)
    {
        _verdict = verdict;
        return this;
    }

    public MathStepGradeBuilder WithAnswer(string answer)
    {
        _answer = answer;
        return this;
    }

    public MathStepGradeBuilder WithMaxScore(int maxScore)
    {
        _maxScore = maxScore;
        return this;
    }

    public MathStepGradeBuilder WithTimeTaken(int milliseconds)
    {
        _timeTakenMilliseconds = milliseconds;
        return this;
    }

    public MathStepGradeBuilder RequestedAt(DateTimeOffset requestedAt)
    {
        _requestedAt = requestedAt;
        return this;
    }

    public MathStepGrade Build() => MathStepGrade.Request(_studentId, _sessionId, _subjectId, _questionId, _questionVersion, _maxScore, _answer, _verdict, _requestedAt, _timeTakenMilliseconds);

    public static MathStepAssessment Assessment(decimal confidence = 0.9m, int points = 2) => new([new MathStepScore(0, "2x = 4", points, 2, "صحيحة"), new MathStepScore(1, "x = 2", 1, 2, "ناقصة")], "جيد", confidence, "claude-sonnet-5", "v1", 900, 150, 0.00495m);
}
