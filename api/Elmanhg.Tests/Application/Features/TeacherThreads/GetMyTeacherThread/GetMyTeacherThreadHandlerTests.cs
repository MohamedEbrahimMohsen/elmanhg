using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherThreads.GetMyTeacherThread;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.GetMyTeacherThread;

public sealed class GetMyTeacherThreadHandlerTests
{
    private static readonly DateTimeOffset Now = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(1);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly TeacherThread _own;
    private readonly TeacherThread _foreign = new TeacherThreadBuilder().Build();
    private readonly GetMyTeacherThreadHandler _handler;

    public GetMyTeacherThreadHandlerTests()
    {
        _own = new TeacherThreadBuilder().ForStudent(_studentId).WithImage("/api/media/teacher-threads/a.png").Build();
        TeacherThread[] threads = [_own, _foreign];
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherThreadRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherThread, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(call => threads.FirstOrDefault(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
        _handler = new GetMyTeacherThreadHandler(_teacherThreadRepository, _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetMyTeacherThreadQuery(_own.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_OwnThread_ReturnsContextAndMessages()
    {
        var result = await _handler.Handle(new GetMyTeacherThreadQuery(_own.Id), TestContext.Current.CancellationToken);

        var context = _own.ReadContext();
        (result.Id, result.Context.SubjectName, result.Context.UnitName, result.Context.LessonName, result.Context.LessonId, result.IsOverdue).Should().Be((_own.Id, "Physics", "Mechanics", "Newton's laws", context.LessonId, false));
        var message = result.Messages.Should().ContainSingle().Subject;
        (message.Id, message.IsFromStudent, message.Kind, message.Text, message.ImageUrl, message.CreatedAt).Should().Be((_own.Messages[0].Id, true, TeacherMessageKind.Text, "Why is F = ma?", "/api/media/teacher-threads/a.png", _own.SubmittedAt));
    }

    [Fact]
    public async Task Handle_ThreadNotFound_ThrowsTeacherThreadNotFound()
    {
        var act = () => _handler.Handle(new GetMyTeacherThreadQuery(_foreign.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadNotFound);
    }
}
