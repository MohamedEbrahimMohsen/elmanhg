using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.GetValidationQueueFilters;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.GetValidationQueueFilters;

public sealed class GetValidationQueueFiltersHandlerTests
{
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly QuestionBuilder _builder = new();
    private readonly GetValidationQueueFiltersHandler _handler;

    public GetValidationQueueFiltersHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.Teacher.Id);
        _teacherSubjectRepository.FindAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>()).Returns([TeacherSubject.Create(_builder.Teacher, _builder.Subject, Guid.NewGuid())]);
        _subjectRepository.FindAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>()).Returns([_builder.Subject]);
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns([_builder.Unit]);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns([_builder.Lesson]);
        _handler = new GetValidationQueueFiltersHandler(_teacherSubjectRepository, _subjectRepository, _unitRepository, _lessonRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_Teacher_ReturnsAssignedSubjectsUnitsAndLessons()
    {
        var result = await _handler.Handle(new GetValidationQueueFiltersQuery(), TestContext.Current.CancellationToken);

        result.Subjects.Should().ContainSingle().Which.Name.Should().Be("Physics");
        var unit = result.Units.Should().ContainSingle().Subject;
        unit.Id.Should().Be(_builder.Unit.Id);
        unit.SubjectId.Should().Be(_builder.Subject.Id);
        var lesson = result.Lessons.Should().ContainSingle().Subject;
        lesson.Id.Should().Be(_builder.Lesson.Id);
        lesson.UnitId.Should().Be(_builder.Unit.Id);
        lesson.Name.Should().Be("Newton's laws");
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetValidationQueueFiltersQuery(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }
}
