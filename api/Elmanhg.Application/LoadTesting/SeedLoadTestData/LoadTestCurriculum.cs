using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.LoadTesting.SeedLoadTestData;

public static class LoadTestCurriculum
{
    public const int UnitCount = 3;
    public const int LessonsPerUnit = 5;
    public const int QuestionsPerLesson = 30;
    public const int ExamQuestionCount = 20;
    public const int ExamTimeLimitMinutes = 30;
    public const int ExamPassMark = 50;

    public static LoadTestCurriculumSet Build(string key, int subjectOrder, User teacher)
    {
        var actor = teacher.Id;
        var subject = Subject.Create(LoadTestData.SubjectName(key), subjectOrder, actor);
        var assignment = TeacherSubject.Create(teacher, subject, actor);
        var set = new LoadTestCurriculumSet(subject, assignment, [], [], [], []);
        for (var unitOrder = 1; unitOrder <= UnitCount; unitOrder++)
        {
            AddUnit(set, unitOrder, actor);
        }

        return set;
    }

    private static void AddUnit(LoadTestCurriculumSet set, int unitOrder, Guid actor)
    {
        var unit = CurriculumUnit.Create(set.Subject, $"الوحدة {unitOrder}", unitOrder, actor);
        set.Units.Add(unit);
        for (var lessonOrder = 1; lessonOrder <= LessonsPerUnit; lessonOrder++)
        {
            var lesson = PublishedLesson(unit, unitOrder, lessonOrder, actor);
            set.Lessons.Add(lesson);
            set.Questions.AddRange(ApprovedQuestions(lesson, unit, set.Assignment, actor));
        }

        var shape = new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, ExamQuestionCount)], null, ExamTimeLimitMinutes, ExamPassMark);
        var servable = new Dictionary<QuestionType, int> { [QuestionType.Mcq] = LessonsPerUnit * QuestionsPerLesson };
        set.Blueprints.Add(ExamBlueprint.CreateForUnit(unit, shape, servable, actor));
    }

    private static Lesson PublishedLesson(CurriculumUnit unit, int unitOrder, int lessonOrder, Guid actor)
    {
        var name = $"الدرس {unitOrder}.{lessonOrder}";
        var lesson = Lesson.Create(unit, name, lessonOrder, actor);
        lesson.Update(name, LoadTestLessonContent.Explanation(lessonOrder), LoadTestLessonContent.Summary(), null, LoadTestLessonContent.Objectives(), actor);
        lesson.Publish(actor);
        return lesson;
    }

    private static IEnumerable<Question> ApprovedQuestions(Lesson lesson, CurriculumUnit unit, TeacherSubject assignment, Guid actor)
    {
        for (var index = 0; index < QuestionsPerLesson; index++)
        {
            var metadata = new QuestionMetadata(LoadTestLessonContent.Difficulty(index), lesson.Objectives[index % lesson.Objectives.Count].Id, []);
            var question = Question.Create(lesson, unit, QuestionType.Mcq, LoadTestLessonContent.Mcq(index), metadata, actor);
            question.Approve(assignment, question.Version);
            yield return question;
        }
    }
}
