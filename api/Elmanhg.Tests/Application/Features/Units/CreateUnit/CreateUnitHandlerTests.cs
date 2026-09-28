using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Units.CreateUnit;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Units.CreateUnit;

public sealed class CreateUnitHandlerTests
{
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly CreateUnitHandler _handler;
    private CurriculumUnit? _added;

    public CreateUnitHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _subjectRepository.GetByIdAsync(_subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_subject);
        _unitRepository.AddAsync(Arg.Do<CurriculumUnit>(x => _added = x), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _handler = new CreateUnitHandler(_subjectRepository, _unitRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ExistingUnits_AddsUnitAfterLastAndSaves()
    {
        var last = CurriculumUnit.Create(_subject, "Waves", 2, Guid.NewGuid());
        _unitRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(last);

        var result = await _handler.Handle(new CreateUnitCommand(_subject.Id, "Optics"), TestContext.Current.CancellationToken);

        _added.Should().NotBeNull();
        _added!.Order.Should().Be(3);
        _added.SubjectId.Should().Be(_subject.Id);
        _added.CreatedBy.Should().Be(_currentUserId);
        result.Id.Should().Be(_added.Id);
        await _unitRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubjectNotFound_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new CreateUnitCommand(Guid.NewGuid(), "Optics"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
        await _unitRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new CreateUnitCommand(_subject.Id, "Optics"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _unitRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
