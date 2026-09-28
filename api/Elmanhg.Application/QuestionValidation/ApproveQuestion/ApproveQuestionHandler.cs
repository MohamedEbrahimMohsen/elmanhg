using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Teachers;
using MediatR;

namespace Elmanhg.Application.QuestionValidation.ApproveQuestion;

public sealed class ApproveQuestionHandler(IQuestionRepository questionRepository, ITeacherSubjectRepository teacherSubjectRepository, ICurrentUserService currentUserService) : IRequestHandler<ApproveQuestionCommand>
{
    public async Task Handle(ApproveQuestionCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var question = await questionRepository.GetByIdAsync(request.QuestionId, cancellationToken).ConfigureAwait(false);
        if (question is null)
        {
            throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        }

        var assignment = await teacherSubjectRepository.FirstOrDefaultAsync(x => x.TeacherId == userId && x.SubjectId == question.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (assignment is null)
        {
            throw new ForbiddenCoreException(ErrorCodes.SubjectOutOfScope);
        }

        question.Approve(assignment, request.Version, request.Difficulty);

        await questionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
