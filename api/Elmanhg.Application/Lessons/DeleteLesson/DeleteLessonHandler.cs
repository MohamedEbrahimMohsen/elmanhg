using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using MediatR;

namespace Elmanhg.Application.Lessons.DeleteLesson;

public sealed class DeleteLessonHandler(ILessonRepository lessonRepository, IQuestionRepository questionRepository, ICurrentUserService currentUserService) : IRequestHandler<DeleteLessonCommand>
{
    public async Task Handle(DeleteLessonCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var lesson = await lessonRepository.GetWithObjectivesAsync(request.LessonId, asNoTracking: false, cancellationToken).ConfigureAwait(false);
        if (lesson is null)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var hasQuestions = await questionRepository.AnyInLessonAsync(lesson.Id, cancellationToken).ConfigureAwait(false);
        lesson.Delete(hasQuestions, userId);

        await lessonRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
