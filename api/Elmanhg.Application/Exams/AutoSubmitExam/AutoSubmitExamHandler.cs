using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Exams.AutoSubmitExam;

public sealed class AutoSubmitExamHandler(ISessionRepository sessionRepository, IQuestionRepository questionRepository, IQuestionMasteryRepository questionMasteryRepository, IOptions<ExamsOptions> examsOptions, IOptions<MasteryOptions> masteryOptions, TimeProvider timeProvider) : IRequestHandler<AutoSubmitExamCommand>
{
    public async Task Handle(AutoSubmitExamCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var session = await sessionRepository.FirstOrDefaultAsync(x => x.Id == request.SessionId && x.Kind != SessionKind.Quiz && x.SubmittedAt == null, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery()).ConfigureAwait(false);
        if (session is null || !session.IsPastDeadline(now, examsOptions.Value.DeadlineGrace))
        {
            return;
        }

        var revisions = await questionRepository.GetRevisionsAsync(session.Items.Select(x => x.QuestionId).ToList(), cancellationToken).ConfigureAwait(false);
        await ExamSubmission.SubmitAsync(session, revisions, questionMasteryRepository, masteryOptions.Value.CorrectThreshold, now, cancellationToken).ConfigureAwait(false);

        await sessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
