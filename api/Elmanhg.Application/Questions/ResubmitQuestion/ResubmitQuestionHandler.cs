using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using MediatR;

namespace Elmanhg.Application.Questions.ResubmitQuestion;

public sealed class ResubmitQuestionHandler(IQuestionRepository questionRepository, ILessonRepository lessonRepository, IRichTextSanitizer richTextSanitizer, ICurrentUserService currentUserService) : IRequestHandler<ResubmitQuestionCommand>
{
    public async Task Handle(ResubmitQuestionCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var question = await questionRepository.GetByIdAsync(request.QuestionId, cancellationToken).ConfigureAwait(false);
        if (question is null)
        {
            throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        }

        var lesson = await lessonRepository.GetWithObjectivesAsync(question.LessonId, asNoTracking: true, cancellationToken).ConfigureAwait(false);
        if (lesson is null)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var content = QuestionContentFactory.CreateContent(request.Question, richTextSanitizer);
        var metadata = QuestionContentFactory.CreateMetadata(request.Question);
        question.Resubmit(request.Question.Type.GetValueOrDefault(), content, metadata, lesson, currentUserService.UserId.Value);

        await questionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
