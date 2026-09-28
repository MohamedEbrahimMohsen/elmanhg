using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.StartReviewSession;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ReviewSessions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.StartReviewSession;

public sealed class StartReviewSessionHandlerTests
{
    private readonly IReviewSessionRepository _reviewSessionRepository = Substitute.For<IReviewSessionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly StartReviewSessionHandler _handler;

    public StartReviewSessionHandlerTests()
    {
        _currentUserService.UserId.Returns(_teacherId);
        _handler = new StartReviewSessionHandler(_reviewSessionRepository, Options.Create(new QuestionValidationOptions()), _currentUserService);
    }

    [Fact]
    public async Task Handle_Teacher_AddsSessionWithConfiguredLifetimeAndSaves()
    {
        ReviewSession? added = null;
        await _reviewSessionRepository.AddAsync(Arg.Do<ReviewSession>(x => added = x), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(new StartReviewSessionCommand(), TestContext.Current.CancellationToken);

        added.Should().NotBeNull();
        added!.TeacherId.Should().Be(_teacherId);
        result.ReviewSessionId.Should().Be(added.Id);
        result.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(480), TimeSpan.FromSeconds(5));
        await _reviewSessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new StartReviewSessionCommand(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _reviewSessionRepository.DidNotReceive().AddAsync(Arg.Any<ReviewSession>(), Arg.Any<CancellationToken>());
        await _reviewSessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
