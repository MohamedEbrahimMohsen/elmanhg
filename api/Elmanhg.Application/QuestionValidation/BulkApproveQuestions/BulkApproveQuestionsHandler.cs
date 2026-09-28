using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.ReviewSessions;
using Elmanhg.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.QuestionValidation.BulkApproveQuestions;

public sealed class BulkApproveQuestionsHandler(IReviewSessionRepository reviewSessionRepository, IQuestionRepository questionRepository, ITeacherSubjectRepository teacherSubjectRepository, ICurrentUserService currentUserService) : IRequestHandler<BulkApproveQuestionsCommand, BulkApproveQuestionsResult>
{
    public async Task<BulkApproveQuestionsResult> Handle(BulkApproveQuestionsCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var session = await reviewSessionRepository.GetByIdAsync(request.ReviewSessionId, cancellationToken, include: query => query.Include(x => x.Openings), asNoTracking: true).ConfigureAwait(false);
        if (session is null || session.TeacherId != userId)
        {
            throw new NotFoundCoreException(ErrorCodes.ReviewSessionNotFound);
        }

        var questionIds = request.QuestionIds.ToList();
        var questions = await questionRepository.FindAsync(x => questionIds.Contains(x.Id), cancellationToken).ConfigureAwait(false);
        if (questions.Count != questionIds.Count)
        {
            throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        }

        var subjectIds = questions
            .Select(x => x.SubjectId)
            .Distinct()
            .ToList();
        var assignments = await teacherSubjectRepository.FindAsync(x => x.TeacherId == userId && subjectIds.Contains(x.SubjectId), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var assignmentsBySubject = assignments.ToDictionary(x => x.SubjectId);
        var questionsById = questions.ToDictionary(x => x.Id);
        foreach (var questionId in questionIds)
        {
            var question = questionsById[questionId];
            if (!assignmentsBySubject.TryGetValue(question.SubjectId, out var assignment))
            {
                throw new ForbiddenCoreException(ErrorCodes.SubjectOutOfScope);
            }

            session.EnsureOpened(question);
            question.Approve(assignment, question.Version);
        }

        await questionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new BulkApproveQuestionsResult(questions.Count);
    }
}
