using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherThreads.RateTeacherThread;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.RateTeacherThread;

public sealed class RateTeacherThreadHandlerTests
{
    private static readonly DateTimeOffset Now = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(5);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly List<TeacherThread> _threads = [];
    private readonly RateTeacherThreadHandler _handler;

    public RateTeacherThreadHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherThreadRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherThread, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(call => _threads.FirstOrDefault(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
        _handler = new RateTeacherThreadHandler(_teacherThreadRepository, _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_AnsweredThread_ClosesAndReturnsRating()
    {
        var thread = Seed(new TeacherThreadBuilder().ForStudent(_studentId).AnsweredBy(_teacherId));

        var result = await Handle(thread.Id, 5);

        (result.Rating, result.Status, result.ClosedAt, result.CanRate, result.CanFollowUp).Should().Be(((int?)5, TeacherThreadStatus.Closed, (DateTimeOffset?)Now, false, false));
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
        foreign.Rating.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AlreadyRated_ThrowsAlreadyRated()
    {
        var thread = Seed(new TeacherThreadBuilder().ForStudent(_studentId).AnsweredBy(_teacherId).Rated(2));

        await ShouldThrow<ConflictCoreException>(thread.Id, DomainErrorCodes.TeacherThreadAlreadyRated);
        thread.Rating.Should().Be(2);
    }

    private TeacherThread Seed(TeacherThreadBuilder builder)
    {
        var thread = builder.Build();
        _threads.Add(thread);
        return thread;
    }

    private Task<Elmanhg.Application.TeacherThreads.Shared.TeacherThreadResult> Handle(Guid threadId, int rating) => _handler.Handle(new RateTeacherThreadCommand(threadId, rating), TestContext.Current.CancellationToken);

    private async Task ShouldThrow<TException>(Guid threadId, string errorCode) where TException : BaseException
    {
        var act = () => Handle(threadId, 4);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
