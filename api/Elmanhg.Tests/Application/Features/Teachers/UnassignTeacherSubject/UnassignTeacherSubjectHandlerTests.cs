using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Teachers.UnassignTeacherSubject;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Teachers.UnassignTeacherSubject;

public sealed class UnassignTeacherSubjectHandlerTests
{
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly UnassignTeacherSubjectHandler _handler;

    public UnassignTeacherSubjectHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _handler = new UnassignTeacherSubjectHandler(_teacherSubjectRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_Assigned_SoftDeletesAndSaves()
    {
        var teacherSubject = TeacherSubject.Create(User.CreateTeacher("Teacher", "teacher@elmanhg.test"), Subject.Create("Physics", 1, Guid.NewGuid()), Guid.NewGuid());
        ArrangeLookup(teacherSubject);

        await _handler.Handle(new UnassignTeacherSubjectCommand(teacherSubject.TeacherId, teacherSubject.SubjectId), TestContext.Current.CancellationToken);

        teacherSubject.IsDeleted.Should().BeTrue();
        teacherSubject.UpdatedBy.Should().Be(_currentUserId);
        await _teacherSubjectRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new UnassignTeacherSubjectCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _teacherSubjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotAssigned_ThrowsTeacherSubjectNotAssigned()
    {
        ArrangeLookup(null);

        var act = () => _handler.Handle(new UnassignTeacherSubjectCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TeacherSubjectNotAssigned);
        await _teacherSubjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private void ArrangeLookup(TeacherSubject? teacherSubject)
    {
        _teacherSubjectRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>()).Returns(teacherSubject);
    }
}
