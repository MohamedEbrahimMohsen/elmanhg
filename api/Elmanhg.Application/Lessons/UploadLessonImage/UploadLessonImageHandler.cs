using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Domain.Lessons;
using MediatR;

namespace Elmanhg.Application.Lessons.UploadLessonImage;

public sealed class UploadLessonImageHandler(ILessonRepository lessonRepository, IFileStorage fileStorage, ICurrentUserService currentUserService) : IRequestHandler<UploadLessonImageCommand, UploadLessonImageResult>
{
    public async Task<UploadLessonImageResult> Handle(UploadLessonImageCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var lesson = await lessonRepository.GetByIdAsync(request.LessonId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (lesson is null)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var file = request.File!;
        var key = $"lessons/{lesson.Id}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var content = file.OpenReadStream();
        var url = await fileStorage.SaveAsync(content, key, cancellationToken).ConfigureAwait(false);

        return new UploadLessonImageResult(url);
    }
}
