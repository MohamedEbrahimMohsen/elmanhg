using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subjects.DeleteSubject;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Subjects.DeleteSubject;

public sealed class DeleteSubjectHandlerTests
{
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly DeleteSubjectHandler _handler;

    public DeleteSubjectHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _subjectRepository.GetByIdAsync(_subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_subject);
        _handler = new DeleteSubjectHandler(_subjectRepository, _unitRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_SubjectWithoutUnits_SoftDeletesAndSaves()
    {
        _unitRepository.AnyInSubjectAsync(_subject.Id, Arg.Any<CancellationToken>()).Returns(false);

        await _handler.Handle(new DeleteSubjectCommand(_subject.Id), TestContext.Current.CancellationToken);

        _subject.IsDeleted.Should().BeTrue();
        _subject.UpdatedBy.Should().Be(_currentUserId);
        await _subjectRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubjectHasUnits_ThrowsSubjectHasUnits()
    {
        _unitRepository.AnyInSubjectAsync(_subject.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => _handler.Handle(new DeleteSubjectCommand(_subject.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.SubjectHasUnits);
        _subject.IsDeleted.Should().BeFalse();
        await _subjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubjectNotFound_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new DeleteSubjectCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
        await _subjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new DeleteSubjectCommand(_subject.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _subjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
