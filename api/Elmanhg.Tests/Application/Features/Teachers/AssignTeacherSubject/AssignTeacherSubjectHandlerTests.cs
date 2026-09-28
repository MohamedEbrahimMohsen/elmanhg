using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Teachers.AssignTeacherSubject;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Application.Features.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Teachers.AssignTeacherSubject;

public sealed class AssignTeacherSubjectHandlerTests
{
    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly User _teacher = User.CreateTeacher("Teacher", "teacher@elmanhg.test");
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly AssignTeacherSubjectHandler _handler;

    public AssignTeacherSubjectHandlerTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userManager.FindByIdAsync(_teacher.Id.ToString()).Returns(_teacher);
        _subjectRepository.GetByIdAsync(_subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_subject);
        _handler = new AssignTeacherSubjectHandler(_userManager, _subjectRepository, _teacherSubjectRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ValidTeacherAndSubject_ReturnsResultAndSaves()
    {
        var result = await _handler.Handle(new AssignTeacherSubjectCommand(_teacher.Id, _subject.Id), TestContext.Current.CancellationToken);

        result.TeacherId.Should().Be(_teacher.Id);
        result.SubjectId.Should().Be(_subject.Id);
        await _teacherSubjectRepository.Received(1).AddAsync(Arg.Is<TeacherSubject>(x => x.TeacherId == _teacher.Id && x.SubjectId == _subject.Id), Arg.Any<CancellationToken>());
        await _teacherSubjectRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new AssignTeacherSubjectCommand(_teacher.Id, _subject.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _teacherSubjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TeacherNotFound_ThrowsUserNotFound()
    {
        var act = () => _handler.Handle(new AssignTeacherSubjectCommand(Guid.NewGuid(), _subject.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotFound);
        await _teacherSubjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubjectNotFound_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new AssignTeacherSubjectCommand(_teacher.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
        await _teacherSubjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyAssigned_ThrowsTeacherSubjectAlreadyAssigned()
    {
        _teacherSubjectRepository.IsAssignedAsync(_teacher.Id, _subject.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => _handler.Handle(new AssignTeacherSubjectCommand(_teacher.Id, _subject.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TeacherSubjectAlreadyAssigned);
        await _teacherSubjectRepository.DidNotReceive().AddAsync(Arg.Any<TeacherSubject>(), Arg.Any<CancellationToken>());
        await _teacherSubjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserIsStudent_ThrowsUserNotTeacher()
    {
        var student = User.CreateStudentWithEmail("Student", "student@elmanhg.test");
        _userManager.FindByIdAsync(student.Id.ToString()).Returns(student);

        var act = () => _handler.Handle(new AssignTeacherSubjectCommand(student.Id, _subject.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.UserNotTeacher);
        await _teacherSubjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
