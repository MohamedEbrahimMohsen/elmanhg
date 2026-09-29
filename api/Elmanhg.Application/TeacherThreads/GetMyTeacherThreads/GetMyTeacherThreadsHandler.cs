using Core.DDD.Models;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.TeacherThreads.GetMyTeacherThreads;

public sealed class GetMyTeacherThreadsHandler(ITeacherThreadRepository teacherThreadRepository, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetMyTeacherThreadsQuery, PageData<TeacherThreadSummaryResult>>
{
    public async Task<PageData<TeacherThreadSummaryResult>> Handle(GetMyTeacherThreadsQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var page = await teacherThreadRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: x => x.StudentId == userId, include: query => query.Include(x => x.Messages), orderBy: query => query.OrderByDescending(x => x.SubmittedAt).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();

        return new PageData<TeacherThreadSummaryResult>
        {
            Items = page.Items
                .Select(x => TeacherThreadResultGenerator.GenerateSummary(x, now))
                .ToList(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalItems = page.TotalItems,
            TotalPages = page.TotalPages,
        };
    }
}
