using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.TeacherThreads.RateTeacherThread;

public sealed class RateTeacherThreadHandler(ITeacherThreadRepository teacherThreadRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<RateTeacherThreadCommand, TeacherThreadResult>
{
    public async Task<TeacherThreadResult> Handle(RateTeacherThreadCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var thread = await teacherThreadRepository.FirstOrDefaultAsync(x => x.Id == request.ThreadId && x.StudentId == userId, cancellationToken, include: query => query.Include(x => x.Messages)).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.TeacherThreadNotFound);

        var now = timeProvider.GetUtcNow();
        thread.Rate(request.Rating, now);

        await teacherThreadRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TeacherThreadResultGenerator.Generate(thread, now);
    }
}
