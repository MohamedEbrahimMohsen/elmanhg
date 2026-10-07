using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Core.Storage;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using MediatR;

namespace Elmanhg.Application.Lessons.UploadLessonImage;

public sealed class UploadLessonImageHandler(ILessonRepository lessonRepository, IFileStorage fileStorage, ICurrentUserService currentUserService) : IRequestHandler<UploadLessonImageCommand, UploadLessonImageResult>
{
    public async Task<UploadLessonImageResult> Handle(UploadLessonImageCommand request, CancellationToken cancellationToken)
    {
        _ = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var lesson = await lessonRepository.GetRequiredAsync(request.LessonId, ErrorCodes.LessonNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        var file = request.File!;
        var key = $"lessons/{lesson.Id}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var content = file.OpenReadStream();
        var url = await fileStorage.SaveAsync(content, key, cancellationToken).ConfigureAwait(false);

        return new UploadLessonImageResult(url);
    }
}
