using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.TeacherThreads.CanViewTeacherThreadImage;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.CanViewTeacherThreadImage;

public sealed class CanViewTeacherThreadImageHandlerTests
{
    private const string ImageUrl = "/api/media/teacher-threads/0123456789abcdef0123456789abcdef.png";
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly TeacherThread _thread;
    private readonly CanViewTeacherThreadImageHandler _handler;

    public CanViewTeacherThreadImageHandlerTests()
    {
        _thread = new TeacherThreadBuilder().ForStudent(_studentId).WithImage(ImageUrl).Build();
        TeacherThread[] threads = [_thread, new TeacherThreadBuilder().Build()];
        _teacherThreadRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherThread, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(call => threads.FirstOrDefault(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
        _handler = new CanViewTeacherThreadImageHandler(_teacherThreadRepository, _teacherSubjectRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ReturnsFalse()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        (await Handle(ImageUrl)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OwningStudent_ReturnsTrue()
    {
        SignIn(_studentId, "Student");

        (await Handle(ImageUrl)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OtherStudent_ReturnsFalse()
    {
        SignIn(_callerId, "Student");

        (await Handle(ImageUrl)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UnknownImage_ReturnsFalse()
    {
        SignIn(_studentId, "Student");

        (await Handle("/api/media/teacher-threads/unknown.png")).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Admin_ReturnsTrue()
    {
        SignIn(_callerId, "Admin");

        (await Handle(ImageUrl)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_TeacherScopedToTheSubject_ReturnsTrue()
    {
        SignIn(_callerId, "Teacher");
        _teacherSubjectRepository.IsAssignedAsync(_callerId, _thread.SubjectId, Arg.Any<CancellationToken>()).Returns(true);

        (await Handle(ImageUrl)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_TeacherOutsideTheSubject_ReturnsFalse()
    {
        SignIn(_callerId, "Teacher");

        (await Handle(ImageUrl)).Should().BeFalse();
    }

    private void SignIn(Guid userId, string role)
    {
        _currentUserService.UserId.Returns(userId);
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(role);
    }

    private Task<bool> Handle(string imageUrl) => _handler.Handle(new CanViewTeacherThreadImageQuery(imageUrl), TestContext.Current.CancellationToken);
}
