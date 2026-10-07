using Core.DDD.Repositories;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using MediatR;

namespace Elmanhg.Application.Questions.UpdateQuestion;

public sealed class UpdateQuestionHandler(IQuestionRepository questionRepository, ILessonRepository lessonRepository, IRichTextSanitizer richTextSanitizer, ICurrentUserService currentUserService) : IRequestHandler<UpdateQuestionCommand>
{
    public async Task Handle(UpdateQuestionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var question = await questionRepository.GetRequiredAsync(request.QuestionId, ErrorCodes.QuestionNotFound, cancellationToken).ConfigureAwait(false);

        var lesson = await lessonRepository.GetWithObjectivesAsync(question.LessonId, asNoTracking: true, cancellationToken).ConfigureAwait(false);
        if (lesson is null)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        QuestionBodyMedia.EnsureLessonMedia(request.Question, lesson.Id);
        var content = QuestionContentFactory.CreateContent(request.Question, richTextSanitizer);
        var metadata = QuestionContentFactory.CreateMetadata(request.Question);
        question.Update(request.Question.Type.GetValueOrDefault(), content, metadata, lesson, userId);

        await questionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
