using Core.DDD.Models;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherThreads.GetMyTeacherThreads;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.GetMyTeacherThreads;

public sealed class GetMyTeacherThreadsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly GetMyTeacherThreadsHandler _handler;

    public GetMyTeacherThreadsHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _handler = new GetMyTeacherThreadsHandler(_teacherThreadRepository, _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetMyTeacherThreadsQuery(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_Threads_ReturnsSummariesWithQuestionTextAndOverdueFlag()
    {
        var thread = new TeacherThreadBuilder().ForStudent(_studentId).SubmittedAt(Now.AddHours(-25)).Build();
        _teacherThreadRepository.FindPaginatedAsync(2, 5, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<TeacherThread, bool>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(new PageData<TeacherThread> { Items = [thread], PageNumber = 2, PageSize = 5, TotalItems = 6, TotalPages = 2 });

        var result = await _handler.Handle(new GetMyTeacherThreadsQuery(2, 5), TestContext.Current.CancellationToken);

        var summary = result.Items.Should().ContainSingle().Subject;
        (summary.Id, summary.SubjectName, summary.LessonName, summary.QuestionText, summary.Status, summary.IsOverdue).Should().Be((thread.Id, "Physics", "Newton's laws", "Why is F = ma?", TeacherThreadStatus.Open, true));
        (result.PageNumber, result.PageSize, result.TotalItems, result.TotalPages).Should().Be((2, 5, 6, 2));
    }

    [Fact]
    public async Task Handle_ThreadWithUnreadReply_ReportsHasUnreadReply()
    {
        var answered = new TeacherThreadBuilder().ForStudent(_studentId).AnsweredBy(Guid.NewGuid()).Build();
        var open = new TeacherThreadBuilder().ForStudent(_studentId).Build();
        _teacherThreadRepository.FindPaginatedAsync(1, 20, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<TeacherThread, bool>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(new PageData<TeacherThread> { Items = [answered, open], PageNumber = 1, PageSize = 20, TotalItems = 2, TotalPages = 1 });

        var result = await _handler.Handle(new GetMyTeacherThreadsQuery(), TestContext.Current.CancellationToken);

        result.Items.Select(x => (x.Id, x.HasUnreadReply)).Should().Equal((answered.Id, true), (open.Id, false));
    }
}
