using Core.DDD.Repositories;
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
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var thread = await teacherThreadRepository.GetRequiredAsync(x => x.Id == request.ThreadId && x.StudentId == userId, ErrorCodes.TeacherThreadNotFound, cancellationToken, include: query => query.Include(x => x.Messages)).ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        thread.Rate(request.Rating, now);

        await teacherThreadRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TeacherThreadResultGenerator.Generate(thread, now);
    }
}
