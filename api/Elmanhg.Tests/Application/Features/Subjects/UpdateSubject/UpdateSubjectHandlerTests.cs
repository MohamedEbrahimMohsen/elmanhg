using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subjects.UpdateSubject;
using Elmanhg.Domain.Subjects;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Subjects.UpdateSubject;

public sealed class UpdateSubjectHandlerTests
{
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly UpdateSubjectHandler _handler;

    public UpdateSubjectHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _handler = new UpdateSubjectHandler(_subjectRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ExistingSubject_RenamesAndSaves()
    {
        var subject = Subject.Create("Physics", 1, Guid.NewGuid());
        ArrangeLookup(subject.Id, subject);

        await _handler.Handle(new UpdateSubjectCommand(subject.Id, "Chemistry"), TestContext.Current.CancellationToken);

        subject.Name.Should().Be("Chemistry");
        subject.UpdatedBy.Should().Be(_currentUserId);
        await _subjectRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubjectNotFound_ThrowsSubjectNotFound()
    {
        var subjectId = Guid.NewGuid();
        ArrangeLookup(subjectId, null);

        var act = () => _handler.Handle(new UpdateSubjectCommand(subjectId, "Chemistry"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
        await _subjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new UpdateSubjectCommand(Guid.NewGuid(), "Chemistry"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _subjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private void ArrangeLookup(Guid subjectId, Subject? subject)
    {
        _subjectRepository.GetByIdAsync(subjectId, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(subject);
    }
}
