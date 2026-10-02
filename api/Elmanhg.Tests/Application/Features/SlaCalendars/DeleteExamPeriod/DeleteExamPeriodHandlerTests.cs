using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.SlaCalendars.DeleteExamPeriod;
using Elmanhg.Domain.SlaCalendars;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.SlaCalendars.DeleteExamPeriod;

public sealed class DeleteExamPeriodHandlerTests
{
    private readonly IExamPeriodRepository _examPeriodRepository = Substitute.For<IExamPeriodRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly DeleteExamPeriodHandler _handler;

    public DeleteExamPeriodHandlerTests()
    {
        _currentUserService.UserId.Returns(_adminId);
        _handler = new DeleteExamPeriodHandler(_examPeriodRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_Existing_SoftDeletesAndSaves()
    {
        var period = Stored();

        await _handler.Handle(new DeleteExamPeriodCommand(period.Id), TestContext.Current.CancellationToken);

        (period.IsDeleted, period.UpdatedBy).Should().Be((true, (Guid?)_adminId));
        await _examPeriodRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Unknown_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new DeleteExamPeriodCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ExamPeriodNotFound);
        await _examPeriodRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUnauthorized()
    {
        var period = Stored();
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new DeleteExamPeriodCommand(period.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        period.IsDeleted.Should().BeFalse();
        await _examPeriodRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private ExamPeriod Stored()
    {
        var period = ExamPeriod.Create("Final exams", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 15), Guid.NewGuid());
        _examPeriodRepository.GetByIdAsync(period.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamPeriod>, IQueryable<ExamPeriod>>?>(), Arg.Any<bool>()).Returns(period);
        return period;
    }
}
