using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Sessions.SubmitAnswer;

public static class QuizEssaySubmission
{
    public static async Task SubmitAsync(Session session, SessionItem item, string essayText, int? reportedTimeTakenMilliseconds, IQuestionRepository questionRepository, IEssayGradeRepository essayGradeRepository, CancellationToken cancellationToken)
    {
        var submission = session.SubmitEssay(item, QuestionSchemaReader.Serialize(new EssayAnswer(essayText)), reportedTimeTakenMilliseconds);
        if (!submission.IsNew)
        {
            return;
        }

        var question = await questionRepository.FirstOrDefaultAsync(x => x.Id == item.QuestionId, cancellationToken, include: query => query.IgnoreQueryFilters(), asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        await essayGradeRepository.AddAsync(EssayGrade.Request(session.StudentId, session.Id, question.SubjectId, item.QuestionId, item.QuestionVersion, item.MaxScore, essayText, submission.SubmittedAt, submission.TimeTakenMilliseconds), cancellationToken).ConfigureAwait(false);
    }
}
