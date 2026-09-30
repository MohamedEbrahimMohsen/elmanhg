using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;

namespace Elmanhg.Tests.Builders;

public sealed class SessionBuilder
{
    public const string AnswerB = "{\"optionId\":\"b\"}";
    public const string AnswerA = "{\"optionId\":\"a\"}";

    public SessionBuilder()
    {
        Questions = new QuestionBuilder();
        Questions.Lesson.Publish(Guid.NewGuid());
    }

    public QuestionBuilder Questions { get; }

    public Guid StudentId { get; } = Guid.NewGuid();

    public List<Question> BuildQuestions(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => Questions.Approved().Build())
            .ToList();
    }

    public Session Build(int count = 2, bool isTestMode = false) => Session.StartQuiz(StudentId, Questions.Lesson, BuildQuestions(count), isTestMode);

    public Session BuildWithEssay(bool isTestMode = false) => Session.StartQuiz(StudentId, Questions.Lesson, [Questions.Approved().Build(), Questions.Essay().Approved().Build()], isTestMode);

    public static QuestionGrade Grade(decimal normalised, GradeFeedback? feedback = null) => QuestionGrade.FromNormalised(new NormalisedGrade(normalised, feedback), 1);
}
