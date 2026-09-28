using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subjects.GetSubjects;
using Elmanhg.Application.Subjects.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.Subjects.GetSubjects;

public sealed class GetSubjectsHandlerTests
{
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Subject _first = Subject.Create("A", 1, Guid.NewGuid());
    private readonly Subject _second = Subject.Create("B", 2, Guid.NewGuid());
    private readonly GetSubjectsHandler _handler;

    public GetSubjectsHandlerTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _unitRepository.CountBySubjectAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int> { [_first.Id] = 2 });
        _handler = new GetSubjectsHandler(_subjectRepository, _teacherSubjectRepository, _unitRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_Admin_ReturnsAllSubjectsWithUnitCounts()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin));
        _subjectRepository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>()).Returns([_first, _second]);

        var result = await _handler.Handle(new GetSubjectsQuery(), TestContext.Current.CancellationToken);

        result.Should().Equal(new SubjectResult(_first.Id, "A", 1, 2), new SubjectResult(_second.Id, "B", 2, 0));
    }

    [Fact]
    public async Task Handle_Teacher_QueriesOnlyAssignedSubjects()
    {
        var teacher = User.CreateTeacher("Teacher", "teacher@elmanhg.test");
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Teacher));
        _teacherSubjectRepository.FindAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>()).Returns([TeacherSubject.Create(teacher, _first, Guid.NewGuid())]);
        _subjectRepository.FindAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>()).Returns([_first]);

        var result = await _handler.Handle(new GetSubjectsQuery(), TestContext.Current.CancellationToken);

        await _subjectRepository.Received(1).FindAsync(Arg.Is<Expression<Func<Subject, bool>>>(e => e.Compile()(_first) && !e.Compile()(_second)), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>());
        await _subjectRepository.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>());
        result.Should().Equal(new SubjectResult(_first.Id, "A", 1, 2));
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetSubjectsQuery(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }
}
