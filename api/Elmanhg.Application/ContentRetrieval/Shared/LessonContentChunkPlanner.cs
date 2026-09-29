using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.ContentRetrieval.Shared;

public static class LessonContentChunkPlanner
{
    public static List<LessonContentChunkDraft> Plan(Lesson lesson, IReadOnlyList<Question> servableQuestions, IRichTextExtractor extractor, int maxCharacters)
    {
        var objectives = lesson.Objectives
            .OrderBy(x => x.Order)
            .Select((x, index) => new RichTextBlock(RichTextBlockKind.Paragraph, $"{index + 1}. {x.Text}"))
            .ToList();
        var questionChunks = servableQuestions
            .SelectMany(x => LessonContentChunker.Chunk(LessonContentSection.QuestionExplanation, x.Id, x.Version, [.. extractor.ExtractBlocks(x.Stem), .. extractor.ExtractBlocks(x.Explanation)], false, maxCharacters));
        return
        [
            .. LessonContentChunker.Chunk(LessonContentSection.Explanation, null, null, extractor.ExtractBlocks(lesson.Explanation), true, maxCharacters),
            .. LessonContentChunker.Chunk(LessonContentSection.Objectives, null, null, objectives, false, maxCharacters),
            .. LessonContentChunker.Chunk(LessonContentSection.Summary, null, null, extractor.ExtractBlocks(lesson.Summary), true, maxCharacters),
            .. questionChunks,
        ];
    }
}
