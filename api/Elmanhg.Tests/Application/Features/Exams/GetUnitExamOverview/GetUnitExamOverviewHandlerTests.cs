using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exams.GetUnitExamOverview;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.Exams.GetUnitExamOverview;

public sealed class GetUnitExamOverviewHandlerTests
{
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly IExamBlueprintRepository _examBlueprintRepository = Substitute.For<IExamBlueprintRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ILessonOpeningRepository _lessonOpeningRepository = Substitute.For<ILessonOpeningRepository>();
    private readonly IOptions<ExamsOptions> _examsOptions = Options.Create(new ExamsOptions());
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ExamSessionBuilder _builder = new();
    private readonly List<ExamBlueprint> _blueprints = [];
    private readonly List<ServableQuestionCount> _counts = [];
    private readonly List<Lesson> _lessons = [];
    private readonly List<LessonOpening> _openings = [];
    private readonly GetUnitExamOverviewHandler _handler;

    public GetUnitExamOverviewHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        _unitRepository.GetByIdAsync(Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(Unit);
        _subjectRepository.GetByIdAsync(_builder.Questions.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_builder.Questions.Subject);
        _examBlueprintRepository.FindAsync(Arg.Any<Expression<Func<ExamBlueprint, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IQueryable<ExamBlueprint>>?>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IOrderedQueryable<ExamBlueprint>>?>(), Arg.Any<bool>())
            .Returns(call => _blueprints.Where(call.Arg<Expression<Func<ExamBlueprint, bool>>>().Compile()).ToList());
        _questionRepository.CountServableByUnitAndTypeAsync(Unit.SubjectId, Arg.Any<CancellationToken>()).Returns(_counts);
        _counts.Add(new ServableQuestionCount(Unit.Id, QuestionType.Mcq, 3));
        SessionRepositoryStub.StubFind(_sessionRepository);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _lessonOpeningRepository.FindAsync(Arg.Any<Expression<Func<LessonOpening, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<LessonOpening>, IQueryable<LessonOpening>>?>(), Arg.Any<Func<IQueryable<LessonOpening>, IOrderedQueryable<LessonOpening>>?>(), Arg.Any<bool>())
            .Returns(call => _openings.Where(call.Arg<Expression<Func<LessonOpening, bool>>>().Compile()).ToList());
        _handler = new GetUnitExamOverviewHandler(_unitRepository, _subjectRepository, _examBlueprintRepository, _questionRepository, _sessionRepository, _lessonRepository, _lessonOpeningRepository, _examsOptions, _currentUserService);
    }

    private CurriculumUnit Unit => _builder.Questions.Unit;

    [Fact]
    public async Task Handle_NoUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetUnitExamOverviewQuery(Unit.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownUnit_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new GetUnitExamOverviewQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
    }

    [Fact]
    public async Task Handle_UnitBlueprint_ReturnsItWithAvailability()
    {
        _blueprints.AddRange([SubjectDefault(), _builder.Blueprint(2)]);

        var result = await _handler.Handle(new GetUnitExamOverviewQuery(Unit.Id), TestContext.Current.CancellationToken);

        result.Blueprint!.IsSubjectDefault.Should().BeFalse();
        result.Blueprint.TypeCounts.Should().ContainSingle().Which.Should().Be(new ExamTypeAvailabilityResult(QuestionType.Mcq, 2, 3));
        result.Blueprint.PassMark.Should().Be(50);
        result.IsAvailable.Should().BeTrue();
        (result.UnitName, result.SubjectName).Should().Be((Unit.Name, _builder.Questions.Subject.Name));
    }

    [Fact]
    public async Task Handle_OnlyDefault_ReturnsDefault()
    {
        _blueprints.Add(SubjectDefault());

        var result = await _handler.Handle(new GetUnitExamOverviewQuery(Unit.Id), TestContext.Current.CancellationToken);

        result.Blueprint!.IsSubjectDefault.Should().BeTrue();
        result.Blueprint.PassMark.Should().Be(60);
    }

    [Fact]
    public async Task Handle_NoBlueprint_ReturnsUnavailable()
    {
        var result = await _handler.Handle(new GetUnitExamOverviewQuery(Unit.Id), TestContext.Current.CancellationToken);

        result.Blueprint.Should().BeNull();
        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShortPool_ReturnsUnavailable()
    {
        _blueprints.Add(_builder.Blueprint(5));

        var result = await _handler.Handle(new GetUnitExamOverviewQuery(Unit.Id), TestContext.Current.CancellationToken);

        result.IsAvailable.Should().BeFalse();
        result.Blueprint!.TypeCounts.Single().Should().Match<ExamTypeAvailabilityResult>(x => x.Available < x.Required);
    }

    [Fact]
    public async Task Handle_OpenExamOtherUnit_ReturnsInProgressElsewhere()
    {
        var other = new ExamSessionBuilder();
        var openExam = Session.StartUnitExam(_builder.StudentId, other.Questions.Unit, other.Blueprint(1), other.BuildQuestions(1), [other.Questions.Lesson], false, ExamSessionBuilder.Now);
        SessionRepositoryStub.StubFind(_sessionRepository, openExam);

        var result = await _handler.Handle(new GetUnitExamOverviewQuery(Unit.Id), TestContext.Current.CancellationToken);

        result.InProgressExam.Should().Be(new InProgressExamResult(openExam.Id, false));
    }

    [Fact]
    public async Task Handle_OpenExamThisUnit_ReturnsInProgressHere()
    {
        var openExam = _builder.Build();
        SessionRepositoryStub.StubFind(_sessionRepository, openExam);

        var result = await _handler.Handle(new GetUnitExamOverviewQuery(Unit.Id), TestContext.Current.CancellationToken);

        result.InProgressExam.Should().Be(new InProgressExamResult(openExam.Id, true));
    }

    [Fact]
    public async Task Handle_GateOff_ReturnsZeroUnopened()
    {
        AddPublishedLessons(2);

        var result = await _handler.Handle(new GetUnitExamOverviewQuery(Unit.Id), TestContext.Current.CancellationToken);

        result.UnopenedLessonCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_GateOnLessonsUnopened_ReturnsUnopenedCount()
    {
        _examsOptions.Value.RequireAllLessonsOpened = true;
        var lessons = AddPublishedLessons(3);
        _openings.Add(LessonOpening.Record(_builder.StudentId, lessons[0], ExamSessionBuilder.Now));

        var result = await _handler.Handle(new GetUnitExamOverviewQuery(Unit.Id), TestContext.Current.CancellationToken);

        result.UnopenedLessonCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_GateOnAdmin_ReturnsZeroUnopened()
    {
        _examsOptions.Value.RequireAllLessonsOpened = true;
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin));
        AddPublishedLessons(2);

        var result = await _handler.Handle(new GetUnitExamOverviewQuery(Unit.Id), TestContext.Current.CancellationToken);

        result.UnopenedLessonCount.Should().Be(0);
    }

    private ExamBlueprint SubjectDefault() => ExamBlueprint.CreateForSubject(_builder.Questions.Subject, new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, 1)], null, null, 60), ExamBlueprintBuilder.Plenty(), Guid.NewGuid());

    private List<Lesson> AddPublishedLessons(int count)
    {
        var lessons = Enumerable.Range(1, count).Select(order => Lesson.Create(Unit, $"Lesson {order}", order, Guid.NewGuid())).ToList();
        lessons.ForEach(x => x.Publish(Guid.NewGuid()));
        _lessons.AddRange(lessons);
        return lessons;
    }
}
