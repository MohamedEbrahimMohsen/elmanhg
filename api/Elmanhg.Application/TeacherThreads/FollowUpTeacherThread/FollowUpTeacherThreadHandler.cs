using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherThreads.FollowUpTeacherThread;

public sealed class FollowUpTeacherThreadHandler(ITeacherThreadRepository teacherThreadRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<FollowUpTeacherThreadCommand, TeacherThreadResult>
{
    public async Task<TeacherThreadResult> Handle(FollowUpTeacherThreadCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var thread = await teacherThreadRepository.FirstOrDefaultAsync(x => x.Id == request.ThreadId && x.StudentId == userId, cancellationToken, include: query => query.Include(x => x.Messages)).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.TeacherThreadNotFound);

        var now = timeProvider.GetUtcNow();
        thread.FollowUp(request.Text ?? string.Empty, now, TimeSpan.FromHours(subscriptionsOptions.Value.AskTeacherReplySlaHours));

        await teacherThreadRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TeacherThreadResultGenerator.Generate(thread, now);
    }
}
