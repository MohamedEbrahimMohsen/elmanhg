using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Core.Settings;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.TeacherThreads.FollowUpTeacherThread;

public sealed class FollowUpTeacherThreadHandler(ITeacherThreadRepository teacherThreadRepository, IExamPeriodRepository examPeriodRepository, IRuntimeSettings runtimeSettings, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<FollowUpTeacherThreadCommand, TeacherThreadResult>
{
    public async Task<TeacherThreadResult> Handle(FollowUpTeacherThreadCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var thread = await teacherThreadRepository.GetRequiredAsync(x => x.Id == request.ThreadId && x.StudentId == userId, ErrorCodes.TeacherThreadNotFound, cancellationToken, include: query => query.Include(x => x.Messages)).ConfigureAwait(false);

        var slaPolicy = await TeacherThreadSlaPolicyLoader.LoadAsync(runtimeSettings, examPeriodRepository, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        thread.FollowUp(request.Text ?? string.Empty, now, slaPolicy);

        await teacherThreadRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TeacherThreadResultGenerator.Generate(thread, now);
    }
}
