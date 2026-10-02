using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.SlaCalendars.CreateExamPeriod;
using Elmanhg.Domain.SlaCalendars;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.SlaCalendars.CreateExamPeriod;

public sealed class CreateExamPeriodHandlerTests
{
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End = new(2026, 7, 15);
    private readonly IExamPeriodRepository _examPeriodRepository = Substitute.For<IExamPeriodRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly CreateExamPeriodHandler _handler;

    public CreateExamPeriodHandlerTests()
    {
        _currentUserService.UserId.Returns(_adminId);
        _handler = new CreateExamPeriodHandler(_examPeriodRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_Valid_AddsTrimmedPeriodAndReturnsResult()
    {
        ExamPeriod? added = null;
        await _examPeriodRepository.AddAsync(Arg.Do<ExamPeriod>(x => added = x), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(new CreateExamPeriodCommand("  Final exams  ", Start, End), TestContext.Current.CancellationToken);

        (result.Name, result.StartDate, result.EndDate).Should().Be(("Final exams", Start, End));
        (added!.Id, added.Name, added.CreatedBy).Should().Be((result.Id, "Final exams", (Guid?)_adminId));
        await _examPeriodRepository.Received(1).AddAsync(Arg.Any<ExamPeriod>(), Arg.Any<CancellationToken>());
        await _examPeriodRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new CreateExamPeriodCommand("Final exams", Start, End), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _examPeriodRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
