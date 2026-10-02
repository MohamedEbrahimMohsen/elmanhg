using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.SlaCalendars.UpdateExamPeriod;
using Elmanhg.Domain.SlaCalendars;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.SlaCalendars.UpdateExamPeriod;

public sealed class UpdateExamPeriodHandlerTests
{
    private static readonly DateOnly Start = new(2026, 6, 1);
    private readonly IExamPeriodRepository _examPeriodRepository = Substitute.For<IExamPeriodRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly UpdateExamPeriodHandler _handler;

    public UpdateExamPeriodHandlerTests()
    {
        _currentUserService.UserId.Returns(_adminId);
        _handler = new UpdateExamPeriodHandler(_examPeriodRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_Existing_UpdatesAndReturnsResult()
    {
        var period = Stored();

        var result = await _handler.Handle(new UpdateExamPeriodCommand(period.Id, " Second round ", Start.AddDays(60), Start.AddDays(70)), TestContext.Current.CancellationToken);

        (result.Id, result.Name, result.StartDate, result.EndDate).Should().Be((period.Id, "Second round", Start.AddDays(60), Start.AddDays(70)));
        (period.Name, period.UpdatedBy).Should().Be(("Second round", (Guid?)_adminId));
        await _examPeriodRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Unknown_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new UpdateExamPeriodCommand(Guid.NewGuid(), "Second round", Start, Start), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ExamPeriodNotFound);
        await _examPeriodRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUnauthorized()
    {
        var period = Stored();
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new UpdateExamPeriodCommand(period.Id, "Second round", Start, Start), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        period.Name.Should().Be("Final exams");
        await _examPeriodRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private ExamPeriod Stored()
    {
        var period = ExamPeriod.Create("Final exams", Start, Start.AddDays(44), Guid.NewGuid());
        _examPeriodRepository.GetByIdAsync(period.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamPeriod>, IQueryable<ExamPeriod>>?>(), Arg.Any<bool>()).Returns(period);
        return period;
    }
}
