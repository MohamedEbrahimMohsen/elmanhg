using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.TeacherThreads.CanViewTeacherThreadMedia;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.CanViewTeacherThreadMedia;

public sealed class CanViewTeacherThreadMediaHandlerTests
{
    private const string ImageUrl = "/api/media/teacher-threads/0123456789abcdef0123456789abcdef.png";
    private const string AudioUrl = "/api/media/teacher-threads/fedcba9876543210fedcba9876543210.webm";
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly TeacherThread _thread;
    private readonly TeacherThread _voiceThread;
    private readonly CanViewTeacherThreadMediaHandler _handler;

    public CanViewTeacherThreadMediaHandlerTests()
    {
        _thread = new TeacherThreadBuilder().ForStudent(_studentId).WithImage(ImageUrl).Build();
        _voiceThread = new TeacherThreadBuilder().ForStudent(_studentId).AnsweredByVoice(Guid.NewGuid(), AudioUrl).Build();
        TeacherThread[] threads = [_thread, new TeacherThreadBuilder().Build(), _voiceThread];
        _teacherThreadRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherThread, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(call => threads.FirstOrDefault(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
        _handler = new CanViewTeacherThreadMediaHandler(_teacherThreadRepository, _teacherSubjectRepository, _currentUserService);
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

    [Fact]
    public async Task Handle_OwningStudentVoiceAudio_ReturnsTrue()
    {
        SignIn(_studentId, "Student");

        (await Handle(AudioUrl)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ScopedTeacherVoiceAudio_ReturnsTrue()
    {
        SignIn(_callerId, "Teacher");
        _teacherSubjectRepository.IsAssignedAsync(_callerId, _voiceThread.SubjectId, Arg.Any<CancellationToken>()).Returns(true);

        (await Handle(AudioUrl)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OtherStudentVoiceAudio_ReturnsFalse()
    {
        SignIn(_callerId, "Student");

        (await Handle(AudioUrl)).Should().BeFalse();
    }

    private void SignIn(Guid userId, string role)
    {
        _currentUserService.UserId.Returns(userId);
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(role);
    }

    private Task<bool> Handle(string imageUrl) => _handler.Handle(new CanViewTeacherThreadMediaQuery(imageUrl), TestContext.Current.CancellationToken);
}
