using Core.Localization;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.Shared.Grading;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Questions.Grading;
using MediatR;

namespace Elmanhg.Application.Questions.GradeQuestionDraft;

public sealed class GradeQuestionDraftHandler(IRichTextSanitizer richTextSanitizer, ILocalizer localizer) : IRequestHandler<GradeQuestionDraftQuery, QuestionGradeResult>
{
    public Task<QuestionGradeResult> Handle(GradeQuestionDraftQuery request, CancellationToken cancellationToken)
    {
        var content = QuestionContentFactory.CreateContent(request.Question, richTextSanitizer);
        var grade = QuestionGrader.Grade(request.Question.Type.GetValueOrDefault(), content.GradingSpec, content.MaxScore, request.Answer);
        return Task.FromResult(new QuestionGradeResult(grade.Score, grade.NormalisedScore, grade.Outcome.ToString(), content.MaxScore, GradeFeedbackText.Localize(grade.Feedback, localizer)));
    }
}
