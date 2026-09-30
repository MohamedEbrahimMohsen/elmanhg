using System.Linq.Expressions;
using Elmanhg.Domain.Lessons;

namespace Elmanhg.Domain.Questions;

// PRD §17 rule 1: the single definition of servable; derived, never stored.
public static class ServableQuestionSpecification
{
    // #126: drag-and-drop has no student canvas or grader yet, so it is never served.
    public static readonly Expression<Func<Question, bool>> QuestionCondition = x => x.ValidationStatus == QuestionValidationStatus.Approved && x.RetiredAt == null && x.Type != QuestionType.DragDrop;

    public static IReadOnlyList<QuestionType> ServedTypes { get; } = [QuestionType.Mcq, QuestionType.Multi, QuestionType.TrueFalse, QuestionType.Fill, QuestionType.Short, QuestionType.Essay];

    public static readonly Expression<Func<Lesson, bool>> LessonCondition = x => x.State == LessonState.Published;

    private static readonly Func<Question, bool> IsQuestionServable = QuestionCondition.Compile();

    private static readonly Func<Lesson, bool> IsLessonServing = LessonCondition.Compile();

    public static bool IsSatisfiedBy(Question question, Lesson lesson) => question.LessonId == lesson.Id && IsQuestionServable(question) && IsLessonServing(lesson);

    public static IQueryable<Question> WhereServable(this IQueryable<Question> questions, IQueryable<Lesson> lessons)
    {
        var servingLessonIds = lessons
            .Where(LessonCondition)
            .Select(x => x.Id);
        return questions
            .Where(QuestionCondition)
            .Where(x => servingLessonIds.Contains(x.LessonId));
    }
}
