using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.Questions.Shared;

public static class QuestionContentFactory
{
    public static QuestionContent CreateContent(QuestionFields fields, IRichTextSanitizer sanitizer)
    {
        var (body, gradingSpec) = QuestionSchemaRules.Normalize(fields, sanitizer);
        return new QuestionContent(sanitizer.Sanitize(fields.Stem), body, gradingSpec, sanitizer.Sanitize(fields.Explanation), fields.MaxScore.GetValueOrDefault());
    }

    public static QuestionMetadata CreateMetadata(QuestionFields fields)
    {
        var tags = fields.Tags
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new QuestionMetadata(fields.Difficulty.GetValueOrDefault(), fields.ObjectiveId, tags);
    }
}
