using Elmanhg.Domain.EssayGrading;

namespace Elmanhg.Tests.Builders;

public sealed class EssayGradeBuilder
{
    public const string DefaultAnswer = "القصور الذاتي هو ممانعة الجسم لتغيير حالته.";
    public static readonly DateTimeOffset DefaultRequestedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private Guid _studentId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private Guid _questionId = Guid.NewGuid();
    private int _questionVersion = 1;
    private readonly int _maxScore = 5;
    private string _answer = DefaultAnswer;
    private DateTimeOffset _requestedAt = DefaultRequestedAt;

    public EssayGradeBuilder ForStudent(Guid studentId)
    {
        _studentId = studentId;
        return this;
    }

    public EssayGradeBuilder ForQuestion(Guid questionId, int version)
    {
        _questionId = questionId;
        _questionVersion = version;
        return this;
    }

    public EssayGradeBuilder RequestedAt(DateTimeOffset requestedAt)
    {
        _requestedAt = requestedAt;
        return this;
    }

    public EssayGradeBuilder WithAnswer(string answer)
    {
        _answer = answer;
        return this;
    }

    public EssayGrade Build() => EssayGrade.Request(_studentId, _sessionId, _subjectId, _questionId, _questionVersion, _maxScore, _answer, _requestedAt);

    public static EssayAssessment Assessment(decimal confidence = 0.9m, int points = 1) => new([new EssayCriterionScore("c1", "Definition", points, 2, "ناقص")], "جيد", confidence, "claude-sonnet-5", "v1", 900, 150, 0.00495m);
}
