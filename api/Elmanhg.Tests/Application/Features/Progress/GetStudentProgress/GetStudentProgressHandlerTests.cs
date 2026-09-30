using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.GetStudentProgress;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Students;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Progress.GetStudentProgress;

public sealed class GetStudentProgressHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly User _student = User.CreateStudentWithPhone("Mona", "01012345678");
    private readonly QuestionBuilder _content = new();
    private readonly GetStudentProgressHandler _handler;

    public GetStudentProgressHandlerTests()
    {
        StudentRepositoryStub.Stub(_userRepository, _student, User.CreateTeacher("Teacher", "teacher@elmanhg.test"));
        _subjectRepository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>()).Returns([_content.Subject]);
        _subjectRepository.FindAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>()).Returns([_content.Subject]);
        _unitRepository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns([_content.Unit]);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns([_content.Lesson]);
        _questionMasteryRepository.GetLessonCountsAsync(_student.Id, null, Arg.Any<CancellationToken>()).Returns([new LessonMasteryCount(_content.Subject.Id, 1, _content.Unit.Id, 1, _content.Lesson.Id, 1, 4, 1, 2)]);
        _questionMasteryRepository.GetObjectiveCountsAsync(_student.Id, Arg.Any<CancellationToken>()).Returns([]);
        _sessionRepository.GetBestExamScoresAsync(_student.Id, Arg.Any<CancellationToken>()).Returns([]);
        _handler = new GetStudentProgressHandler(_userRepository, _questionMasteryRepository, _sessionRepository, _subjectRepository, _unitRepository, _lessonRepository, Options.Create(new ProgressOptions { WeakLessonCount = 4, WeakObjectiveCount = 3 }));
    }

    [Fact]
    public async Task Handle_Student_ReturnsThatStudentsSubjectsAndWeakSpots()
    {
        var result = await _handler.Handle(new GetStudentProgressQuery(_student.Id), TestContext.Current.CancellationToken);

        var subject = result.Subjects.Should().ContainSingle().Subject;
        (subject.SubjectId, subject.ServableCount, subject.MasteredCount, subject.SeenCount, subject.MasteryPercent).Should().Be((_content.Subject.Id, 4, 1, 2, 25));
        var weakLesson = result.WeakSpots.Lessons.Should().ContainSingle().Subject;
        (weakLesson.LessonId, weakLesson.LessonName, weakLesson.MasteryPercent).Should().Be((_content.Lesson.Id, "Newton's laws", 25));
        await _questionMasteryRepository.Received(2).GetLessonCountsAsync(_student.Id, null, Arg.Any<CancellationToken>());
        await _questionMasteryRepository.Received(1).GetObjectiveCountsAsync(_student.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownStudent_ThrowsStudentNotFound()
    {
        var act = () => _handler.Handle(new GetStudentProgressQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.StudentNotFound);
        await _questionMasteryRepository.DidNotReceive().GetLessonCountsAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
}
