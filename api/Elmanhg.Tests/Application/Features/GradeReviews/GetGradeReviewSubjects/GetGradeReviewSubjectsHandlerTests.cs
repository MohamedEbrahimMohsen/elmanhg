using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.GetGradeReviewSubjects;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.GradeReviews.GetGradeReviewSubjects;

public sealed class GetGradeReviewSubjectsHandlerTests
{
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly IEssayGradeRepository _essayGradeRepository = Substitute.For<IEssayGradeRepository>();
    private readonly IMathStepGradeRepository _mathStepGradeRepository = Substitute.For<IMathStepGradeRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly User _teacher = User.CreateTeacher("Teacher", "teacher@example.com");
    private readonly Subject _chemistry = Subject.Create("Chemistry", 2, Guid.NewGuid());
    private readonly Subject _physics = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly Subject _biology = Subject.Create("Biology", 3, Guid.NewGuid());
    private readonly List<TeacherSubject> _assignments = [];

    public GetGradeReviewSubjectsHandlerTests()
    {
        _currentUserService.UserId.Returns(_teacher.Id);
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Teacher));
        _teacherSubjectRepository.FindAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>())
            .Returns(call => _assignments.Where(call.Arg<Expression<Func<TeacherSubject, bool>>>().Compile()).ToList());
        _subjectRepository.FindAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>())
            .Returns(call => new List<Subject> { _chemistry, _physics, _biology }.Where(call.Arg<Expression<Func<Subject, bool>>>().Compile()).ToList());
        _essayGradeRepository.CountInReviewBySubjectAsync(Arg.Any<IReadOnlyCollection<Guid>?>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int> { [_physics.Id] = 2 });
        _mathStepGradeRepository.CountInReviewBySubjectAsync(Arg.Any<IReadOnlyCollection<Guid>?>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int> { [_physics.Id] = 1 });
    }

    [Fact]
    public async Task Handle_Teacher_ReturnsAssignedSubjectsWithCountsInOrder()
    {
        _assignments.AddRange([TeacherSubject.Create(_teacher, _chemistry, Guid.NewGuid()), TeacherSubject.Create(_teacher, _physics, Guid.NewGuid())]);

        var result = await Handle();

        result.Should().Equal(new GradeReviewSubjectResult(_physics.Id, "Physics", 2, 1), new GradeReviewSubjectResult(_chemistry.Id, "Chemistry", 0, 0));
        await _essayGradeRepository.Received(1).CountInReviewBySubjectAsync(Arg.Is<IReadOnlyCollection<Guid>?>(x => x != null && x.Count == 2 && x.Contains(_physics.Id) && x.Contains(_chemistry.Id)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Admin_ReturnsEverySubject()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin));

        var result = await Handle();

        result.Select(x => x.SubjectId).Should().Equal(_physics.Id, _chemistry.Id, _biology.Id);
        await _essayGradeRepository.Received(1).CountInReviewBySubjectAsync(null, Arg.Any<CancellationToken>());
        await _mathStepGradeRepository.Received(1).CountInReviewBySubjectAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = Handle;

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    private Task<List<GradeReviewSubjectResult>> Handle() => new GetGradeReviewSubjectsHandler(_teacherSubjectRepository, _subjectRepository, _essayGradeRepository, _mathStepGradeRepository, _currentUserService).Handle(new GetGradeReviewSubjectsQuery(), TestContext.Current.CancellationToken);
}
