using Core.Errors;
using Core.Localization;
using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.Shared.Grading;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Questions.GradeQuestionDraft;

public sealed class GradeQuestionDraftHandler(IRichTextSanitizer richTextSanitizer, IRichTextExtractor richTextExtractor, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IAiEssayGradingClient essayGradingClient, IOptions<EssayGradingOptions> essayGradingOptions, ILocalizer localizer, IAiMathCheckClient mathCheckClient, IOptions<SessionsOptions> sessionsOptions) : IRequestHandler<GradeQuestionDraftQuery, QuestionGradeResult>
{
    public async Task<QuestionGradeResult> Handle(GradeQuestionDraftQuery request, CancellationToken cancellationToken)
    {
        var content = QuestionContentFactory.CreateContent(request.Question, richTextSanitizer);
        var type = request.Question.Type.GetValueOrDefault();
        if (type != QuestionType.Essay)
        {
            if (QuestionAnswerRules.ExceedsLimits(type, request.Answer, sessionsOptions.Value))
            {
                throw new ApplicationValidationCoreException(ErrorCodes.AttemptAnswerTooLong);
            }

            var grade = await AnswerGrader.GradeAsync(type, content.GradingSpec, content.MaxScore, request.Answer, mathCheckClient, cancellationToken).ConfigureAwait(false);
            return Result(grade, content.MaxScore, null);
        }

        var text = QuestionSchemaReader.Read<EssayAnswer>(request.Answer).Text!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result(QuestionGrade.FromNormalised(NormalisedGrade.Unanswered, content.MaxScore), content.MaxScore, null);
        }

        var context = request.LessonId is { } lessonId ? await EssayGradingContextLoader.LoadAsync(lessonId, lessonRepository, unitRepository, subjectRepository, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.LessonNotFound) : null;
        var aiRequest = EssayGradingRequestFactory.Create(content.Stem, content.GradingSpec, text, context, richTextExtractor, essayGradingOptions.Value.ContextFieldMaxLength);
        var result = await essayGradingClient.GradeAsync(aiRequest, cancellationToken).ConfigureAwait(false);
        var essayGrade = QuestionGrader.GradeEssay(content.GradingSpec, content.MaxScore, EssayAssessments.Awards(result));
        return Result(essayGrade, content.MaxScore, EssayGradeResultGenerator.Detail(EssayAssessments.From(aiRequest, result)));
    }

    private QuestionGradeResult Result(QuestionGrade grade, int maxScore, EssayGradeDetailResult? essay) => new(grade.Score, grade.NormalisedScore, grade.Outcome.ToString(), maxScore, GradeFeedbackText.Localize(grade.Feedback, localizer), essay);
}
