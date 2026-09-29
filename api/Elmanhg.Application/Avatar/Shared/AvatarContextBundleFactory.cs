using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Avatar.Shared;

public static class AvatarContextBundleFactory
{
    public static AiContextBundle ForGlobal(IReadOnlyList<string> subjectNames) => new(AiChatEntryPoint.Global, null, null, null, null, subjectNames);

    public static AiContextBundle ForLesson(AvatarEntryPoint entryPoint, Subject subject, CurriculumUnit unit, Lesson lesson, AiQuestionContext? question, bool hasSources, IRichTextExtractor extractor, int fieldMaxLength)
    {
        var objectives = lesson.Objectives
            .OrderBy(x => x.Order)
            .Select(x => x.Text)
            .ToList();
        var lessonContext = new AiLessonContext(lesson.Id, lesson.Name, hasSources ? string.Empty : AvatarText.Plain(lesson.Explanation, extractor, fieldMaxLength), objectives, hasSources ? string.Empty : AvatarText.Plain(lesson.Summary, extractor, fieldMaxLength));
        return new AiContextBundle(ToAiEntryPoint(entryPoint), new AiContextReference(subject.Id, subject.Name), new AiContextReference(unit.Id, unit.Name), lessonContext, question, []);
    }

    public static AiQuestionContext ForQuestion(Guid questionId, QuestionRevisionSnapshot snapshot, string? answerJson, IRichTextExtractor extractor, int fieldMaxLength)
    {
        var studentAnswer = AvatarAnswerText.StudentAnswer(snapshot, answerJson, extractor);
        var correctAnswer = AvatarAnswerText.CorrectAnswer(snapshot, extractor);
        var explanation = AvatarText.Plain(snapshot.Explanation, extractor, fieldMaxLength);
        return new AiQuestionContext(questionId, AvatarText.Plain(snapshot.Stem, extractor, fieldMaxLength), studentAnswer is null ? null : AvatarText.Truncate(studentAnswer, fieldMaxLength), correctAnswer is null ? null : AvatarText.Truncate(correctAnswer, fieldMaxLength), explanation.Length == 0 ? null : explanation);
    }

    private static AiChatEntryPoint ToAiEntryPoint(AvatarEntryPoint entryPoint) => entryPoint switch
    {
        AvatarEntryPoint.Lesson => AiChatEntryPoint.Lesson,
        AvatarEntryPoint.QuizQuestion => AiChatEntryPoint.QuizQuestion,
        AvatarEntryPoint.ExamReview => AiChatEntryPoint.ExamReview,
        AvatarEntryPoint.Global => AiChatEntryPoint.Global,
        _ => throw new ArgumentOutOfRangeException(nameof(entryPoint)),
    };
}
