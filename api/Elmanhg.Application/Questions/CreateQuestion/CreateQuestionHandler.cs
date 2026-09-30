using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Questions.CreateQuestion;

public sealed class CreateQuestionHandler(ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, IQuestionRepository questionRepository, IRichTextSanitizer richTextSanitizer, ICurrentUserService currentUserService) : IRequestHandler<CreateQuestionCommand, CreateQuestionResult>
{
    public async Task<CreateQuestionResult> Handle(CreateQuestionCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var lesson = await lessonRepository.GetWithObjectivesAsync(request.LessonId, asNoTracking: true, cancellationToken).ConfigureAwait(false);
        if (lesson is null)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var unit = await unitRepository.GetByIdAsync(lesson.UnitId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (unit is null)
        {
            throw new NotFoundCoreException(ErrorCodes.UnitNotFound);
        }

        QuestionBodyMedia.EnsureLessonMedia(request.Question, lesson.Id);
        var content = QuestionContentFactory.CreateContent(request.Question, richTextSanitizer);
        var metadata = QuestionContentFactory.CreateMetadata(request.Question);
        var question = Question.Create(lesson, unit, request.Question.Type.GetValueOrDefault(), content, metadata, currentUserService.UserId.Value);

        await questionRepository.AddAsync(question, cancellationToken).ConfigureAwait(false);
        await questionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreateQuestionResult(question.Id);
    }
}
