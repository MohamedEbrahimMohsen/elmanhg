using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Questions.Grading;
using MediatR;

namespace Elmanhg.Application.Questions.GradeQuestionDraft;

public sealed class GradeQuestionDraftHandler(IRichTextSanitizer richTextSanitizer) : IRequestHandler<GradeQuestionDraftQuery, QuestionGradeResult>
{
    public Task<QuestionGradeResult> Handle(GradeQuestionDraftQuery request, CancellationToken cancellationToken)
    {
        var content = QuestionContentFactory.CreateContent(request.Question, richTextSanitizer);
        var grade = QuestionGrader.Grade(request.Question.Type.GetValueOrDefault(), content.GradingSpec, content.MaxScore, request.Answer);
        return Task.FromResult(new QuestionGradeResult(grade.Score, grade.NormalisedScore, grade.Outcome.ToString(), content.MaxScore));
    }
}
