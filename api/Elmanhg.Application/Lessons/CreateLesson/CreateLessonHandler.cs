using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Lessons.CreateLesson;

public sealed class CreateLessonHandler(ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<CreateLessonCommand, CreateLessonResult>
{
    public async Task<CreateLessonResult> Handle(CreateLessonCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var unit = await unitRepository.GetByIdAsync(request.UnitId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (unit is null)
        {
            throw new NotFoundCoreException(ErrorCodes.UnitNotFound);
        }

        var last = await lessonRepository.FirstOrDefaultAsync(x => x.UnitId == unit.Id, cancellationToken, orderBy: query => query.OrderByDescending(x => x.Order), asNoTracking: true).ConfigureAwait(false);
        var lesson = Lesson.Create(unit, request.Name, (last?.Order ?? 0) + 1, currentUserService.UserId.Value);

        await lessonRepository.AddAsync(lesson, cancellationToken).ConfigureAwait(false);
        await lessonRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreateLessonResult(lesson.Id);
    }
}
