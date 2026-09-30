using Core.Errors;
using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.MathStepGrading.Shared;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.Shared.Grading;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using System.Text.Json;

namespace Elmanhg.Application.Questions.GradeQuestionDraft;

public static class MathStepsDraftGrading
{
    public static async Task<(QuestionGrade Grade, MathStepGradeDetailResult? Detail)> GradeAsync(QuestionContent content, JsonElement answer, Guid? lessonId, IAiMathCheckClient mathCheckClient, IAiMathStepGradingClient mathStepGradingClient, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IRichTextExtractor richTextExtractor, int contextFieldMaxLength, CancellationToken cancellationToken)
    {
        var decision = await AnswerGrader.DecideAsync(QuestionType.MathSteps, content.GradingSpec, content.MaxScore, answer, mathCheckClient, cancellationToken).ConfigureAwait(false);
        if (decision.Grade is not null)
        {
            return (decision.Grade, null);
        }

        if (decision.Verdict is not { } verdict)
        {
            return (QuestionGrader.GradeMathSteps(content.MaxScore, MathAnswerVerdict.Unchecked), null);
        }

        var context = lessonId is { } id ? await EssayGradingContextLoader.LoadAsync(id, lessonRepository, unitRepository, subjectRepository, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.LessonNotFound) : null;
        var spec = JsonSerializer.Deserialize<MathStepsGradingSpec>(content.GradingSpec, QuestionJson.SerializerOptions)!;
        var aiRequest = MathStepGradingRequestFactory.Create(content.Stem, spec, QuestionSchemaReader.Read<MathStepsAnswer>(answer), context, richTextExtractor, contextFieldMaxLength);
        var result = await mathStepGradingClient.GradeAsync(aiRequest, cancellationToken).ConfigureAwait(false);
        var grade = QuestionGrader.GradeMathStepsCombined(content.GradingSpec, content.MaxScore, verdict, MathStepAssessments.Awards(result));
        return (grade, MathStepGradeResultGenerator.Detail(MathStepAssessments.From(aiRequest, result), verdict));
    }
}
