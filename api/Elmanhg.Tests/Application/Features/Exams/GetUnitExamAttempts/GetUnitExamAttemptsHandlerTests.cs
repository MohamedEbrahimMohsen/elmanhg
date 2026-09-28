using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exams.GetUnitExamAttempts;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Exams.GetUnitExamAttempts;

public sealed class GetUnitExamAttemptsHandlerTests
{
    private static readonly DateTimeOffset Day = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ExamSessionBuilder _builder = new();
    private readonly GetUnitExamAttemptsHandler _handler;

    public GetUnitExamAttemptsHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        _unitRepository.GetByIdAsync(Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(Unit);
        _handler = new GetUnitExamAttemptsHandler(_unitRepository, _sessionRepository, _currentUserService);
    }

    private CurriculumUnit Unit => _builder.Questions.Unit;

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetUnitExamAttemptsQuery(Unit.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _sessionRepository.DidNotReceive().GetExamAttemptsAsync(Arg.Any<Guid>(), Arg.Any<SessionKind>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownUnit_ThrowsUnitNotFound()
    {
        var act = () => _handler.Handle(new GetUnitExamAttemptsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
        await _sessionRepository.DidNotReceive().GetExamAttemptsAsync(Arg.Any<Guid>(), Arg.Any<SessionKind>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Unit_ReturnsUnitExamAttemptsForUnitKey()
    {
        List<ExamAttemptSummary> summaries = [new(Guid.NewGuid(), Day.AddDays(1), 40m), new(Guid.NewGuid(), Day, 65m)];
        _sessionRepository.GetExamAttemptsAsync(_builder.StudentId, SessionKind.UnitExam, $"unit:{Unit.Id:D}", Arg.Any<CancellationToken>()).Returns(summaries);

        var result = await _handler.Handle(new GetUnitExamAttemptsQuery(Unit.Id), TestContext.Current.CancellationToken);

        result.Attempts.Select(x => x.SessionId).Should().Equal(summaries.Select(x => x.SessionId));
        result.BestScorePercent.Should().Be(65m);
        result.Attempts.Select(x => x.IsBest).Should().Equal(false, true);
    }
}
