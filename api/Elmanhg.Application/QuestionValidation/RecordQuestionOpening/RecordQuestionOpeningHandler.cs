using Core.DDD.Repositories;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.ReviewSessions;
using Elmanhg.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.QuestionValidation.RecordQuestionOpening;

public sealed class RecordQuestionOpeningHandler(IReviewSessionRepository reviewSessionRepository, IQuestionRepository questionRepository, ITeacherSubjectRepository teacherSubjectRepository, ICurrentUserService currentUserService) : IRequestHandler<RecordQuestionOpeningCommand>
{
    public async Task Handle(RecordQuestionOpeningCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var session = await reviewSessionRepository.GetByIdAsync(request.ReviewSessionId, cancellationToken, include: query => query.Include(x => x.Openings)).ConfigureAwait(false);
        if (session is null || session.TeacherId != userId)
        {
            throw new NotFoundCoreException(ErrorCodes.ReviewSessionNotFound);
        }

        var question = await questionRepository.GetRequiredAsync(request.QuestionId, ErrorCodes.QuestionNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        if (!await teacherSubjectRepository.IsAssignedAsync(userId, question.SubjectId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenCoreException(ErrorCodes.SubjectOutOfScope);
        }

        session.RecordOpening(question);

        await reviewSessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
