using Core.DDD.Models;
using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.GetStudentSessionHistory;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Students;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Progress.GetStudentSessionHistory;

public sealed class GetStudentSessionHistoryHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly SessionBuilder _target = new();
    private readonly SessionBuilder _other = new();
    private readonly User _student = User.CreateStudentWithPhone("Mona", "01012345678");
    private readonly GetStudentSessionHistoryHandler _handler;

    public GetStudentSessionHistoryHandlerTests()
    {
        StudentRepositoryStub.Stub(_userRepository, _student);
        List<Lesson> lessons = [_target.Questions.Lesson, _other.Questions.Lesson];
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _handler = new GetStudentSessionHistoryHandler(_userRepository, _sessionRepository, _lessonRepository, _unitRepository);
    }

    [Fact]
    public async Task Handle_Student_ReturnsOnlyThatStudentsSessions()
    {
        var student = User.CreateStudentWithPhone("Mona", "01012345679");
        student.Id = _target.StudentId;
        StudentRepositoryStub.Stub(_userRepository, student);
        var own = _target.Build();
        List<Session> sessions = [own, _other.Build()];
        _sessionRepository.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Session, bool>>?>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<Func<IQueryable<Session>, IOrderedQueryable<Session>>?>(), Arg.Any<bool>())
            .Returns(call =>
            {
                var items = sessions.Where(call.ArgAt<Expression<Func<Session, bool>>?>(3)!.Compile()).ToList();
                return new PageData<Session> { Items = items, PageNumber = call.ArgAt<int>(0), PageSize = call.ArgAt<int>(1), TotalItems = items.Count, TotalPages = 1 };
            });

        var result = await _handler.Handle(new GetStudentSessionHistoryQuery(student.Id, null), TestContext.Current.CancellationToken);

        var item = result.Items.Should().ContainSingle().Subject;
        (item.Id, item.ScopeName, result.TotalItems).Should().Be((own.Id, "Newton's laws", 1L));
    }

    [Fact]
    public async Task Handle_UnknownStudent_ThrowsStudentNotFound()
    {
        var act = () => _handler.Handle(new GetStudentSessionHistoryQuery(Guid.NewGuid(), null), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.StudentNotFound);
        await _sessionRepository.DidNotReceive().FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Session, bool>>?>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<Func<IQueryable<Session>, IOrderedQueryable<Session>>?>(), Arg.Any<bool>());
    }
}
