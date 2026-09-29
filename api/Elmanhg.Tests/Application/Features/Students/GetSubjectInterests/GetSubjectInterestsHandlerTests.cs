using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Students.GetSubjectInterests;
using Elmanhg.Application.Students.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Students.GetSubjectInterests;

public sealed class GetSubjectInterestsHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly User _student = User.CreateStudentWithEmail("Sara", "sara@elmanhg.test");
    private readonly Subject _physics = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly Subject _chemistry = Subject.Create("Chemistry", 2, Guid.NewGuid());
    private readonly GetSubjectInterestsHandler _handler;

    public GetSubjectInterestsHandlerTests()
    {
        _currentUserService.UserId.Returns(_student.Id);
        _userRepository.GetByIdAsync(_student.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<bool>()).Returns(_student);
        _subjectRepository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>()).Returns([_physics, _chemistry]);
        _handler = new GetSubjectInterestsHandler(_userRepository, _subjectRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetSubjectInterestsQuery(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UserMissing_ThrowsUserNotFound()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());

        var act = () => _handler.Handle(new GetSubjectInterestsQuery(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotFound);
    }

    [Fact]
    public async Task Handle_StudentWithInterests_ReturnsEverySubjectWithSelection()
    {
        _student.ChooseSubjectInterests([_chemistry.Id], new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));

        var result = await _handler.Handle(new GetSubjectInterestsQuery(), TestContext.Current.CancellationToken);

        result.NeedsOnboarding.Should().BeFalse();
        result.Subjects.Should().Equal(new SubjectInterestResult(_physics.Id, "Physics", false), new SubjectInterestResult(_chemistry.Id, "Chemistry", true));
    }

    [Fact]
    public async Task Handle_NewStudent_ReturnsNeedsOnboarding()
    {
        var result = await _handler.Handle(new GetSubjectInterestsQuery(), TestContext.Current.CancellationToken);

        result.NeedsOnboarding.Should().BeTrue();
        result.Subjects.Should().OnlyContain(x => !x.IsSelected).And.HaveCount(2);
    }
}
