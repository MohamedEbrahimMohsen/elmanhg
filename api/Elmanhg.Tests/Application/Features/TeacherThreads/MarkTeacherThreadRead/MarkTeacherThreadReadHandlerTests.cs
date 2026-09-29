using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherThreads.MarkTeacherThreadRead;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.MarkTeacherThreadRead;

public sealed class MarkTeacherThreadReadHandlerTests
{
    private static readonly DateTimeOffset Now = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(3);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly TeacherThread _own;
    private readonly TeacherThread _foreign;
    private readonly MarkTeacherThreadReadHandler _handler;

    public MarkTeacherThreadReadHandlerTests()
    {
        _own = new TeacherThreadBuilder().ForStudent(_studentId).AnsweredBy(_teacherId).Build();
        _foreign = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();
        TeacherThread[] threads = [_own, _foreign];
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherThreadRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherThread, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(call => threads.FirstOrDefault(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
        _handler = new MarkTeacherThreadReadHandler(_teacherThreadRepository, _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new MarkTeacherThreadReadCommand(_own.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OtherStudentsThread_ThrowsTeacherThreadNotFound()
    {
        var act = () => _handler.Handle(new MarkTeacherThreadReadCommand(_foreign.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadNotFound);
        _foreign.HasUnreadReply().Should().BeTrue();
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OwnAnsweredThread_MarksRepliesReadAndSaves()
    {
        await _handler.Handle(new MarkTeacherThreadReadCommand(_own.Id), TestContext.Current.CancellationToken);

        _own.Messages.Single(x => x.SenderId == _teacherId).StudentReadAt.Should().Be(Now);
        _own.HasUnreadReply().Should().BeFalse();
        await _teacherThreadRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
