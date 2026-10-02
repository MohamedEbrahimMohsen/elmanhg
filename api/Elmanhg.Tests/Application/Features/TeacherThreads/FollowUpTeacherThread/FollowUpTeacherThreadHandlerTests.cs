using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.FollowUpTeacherThread;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.FollowUpTeacherThread;

public sealed class FollowUpTeacherThreadHandlerTests
{
    private const string FollowUpText = "Can you show the units?";
    private static readonly DateTimeOffset Now = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(5);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly List<TeacherThread> _threads = [];
    private readonly FollowUpTeacherThreadHandler _handler;

    public FollowUpTeacherThreadHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherThreadRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherThread, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(call => _threads.FirstOrDefault(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
        _handler = new FollowUpTeacherThreadHandler(_teacherThreadRepository, new FakeRuntimeSettings(subscriptions: new SubscriptionsOptions { AskTeacherReplySlaHours = 24 }), _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_AnsweredThread_ReopensAndReturnsResult()
    {
        var thread = Seed(new TeacherThreadBuilder().ForStudent(_studentId).AnsweredBy(_teacherId));

        var result = await Handle(thread.Id);

        (result.Status, result.CanFollowUp, result.CanRate, result.SlaDueAt).Should().Be((TeacherThreadStatus.Open, false, false, Now.AddHours(24)));
        result.Messages.Should().HaveCount(3);
        result.Messages.Last().IsFromStudent.Should().BeTrue();
        await _teacherThreadRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Unauthenticated_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var thread = Seed(new TeacherThreadBuilder().ForStudent(_studentId).AnsweredBy(_teacherId));

        await ShouldThrow<UnauthorizedCoreException>(thread.Id, ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_ThreadNotFound_ThrowsNotFound()
    {
        var foreign = Seed(new TeacherThreadBuilder().AnsweredBy(_teacherId));

        await ShouldThrow<NotFoundCoreException>(foreign.Id, ErrorCodes.TeacherThreadNotFound);
        foreign.Status.Should().Be(TeacherThreadStatus.Answered);
    }

    [Fact]
    public async Task Handle_OpenThread_ThrowsFollowUpNotAllowed()
    {
        var thread = Seed(new TeacherThreadBuilder().ForStudent(_studentId).ClaimedBy(_teacherId));

        await ShouldThrow<ConflictCoreException>(thread.Id, DomainErrorCodes.TeacherThreadFollowUpNotAllowed);
    }

    private TeacherThread Seed(TeacherThreadBuilder builder)
    {
        var thread = builder.Build();
        _threads.Add(thread);
        return thread;
    }

    private Task<Elmanhg.Application.TeacherThreads.Shared.TeacherThreadResult> Handle(Guid threadId) => _handler.Handle(new FollowUpTeacherThreadCommand(threadId, FollowUpText), TestContext.Current.CancellationToken);

    private async Task ShouldThrow<TException>(Guid threadId, string errorCode) where TException : BaseException
    {
        var act = () => Handle(threadId);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
