using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exams.StartUnitExam;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.Exams.StartUnitExam;

public sealed class StartUnitExamFreeTierTests
{
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ILessonOpeningRepository _lessonOpeningRepository = Substitute.For<ILessonOpeningRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IExamBlueprintRepository _examBlueprintRepository = Substitute.For<IExamBlueprintRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ExamSessionBuilder _builder = new();
    private readonly List<Question> _pool;
    private readonly List<ExamBlueprint> _blueprints = [];
    private readonly List<QuestionMastery> _masteries = [];
    private readonly List<LessonOpening> _openings = [];
    private readonly IOptions<ExamsOptions> _examsOptions = Options.Create(new ExamsOptions());
    private readonly StartUnitExamHandler _handler;

    public StartUnitExamFreeTierTests()
    {
        _pool = _builder.BuildQuestions(3);
        _blueprints.Add(_builder.Blueprint(2));
        _currentUserService.UserId.Returns(_builder.StudentId);
        _timeProvider.GetUtcNow().Returns(ExamSessionBuilder.Now.AddMinutes(1));
        _unitRepository.GetByIdAsync(Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(Unit);
        _subjectRepository.GetByIdAsync(Unit.SubjectId, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_builder.Questions.Subject);
        SessionRepositoryStub.StubFind(_sessionRepository);
        _examBlueprintRepository.FindAsync(Arg.Any<Expression<Func<ExamBlueprint, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IQueryable<ExamBlueprint>>?>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IOrderedQueryable<ExamBlueprint>>?>(), Arg.Any<bool>())
            .Returns(call => _blueprints.Where(call.Arg<Expression<Func<ExamBlueprint, bool>>>().Compile()).ToList());
        _questionRepository.GetServableExamCandidatesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(call => _pool.Select(x => new ExamCandidate(x.Id, x.LessonId, x.Type, x.Difficulty)).ToList());
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => _pool.Where(call.Arg<Expression<Func<Question, bool>>>().Compile()).ToList());
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_pool.SelectMany(x => x.Revisions).ToList());
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => new List<Lesson> { _builder.Questions.Lesson }.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _lessonOpeningRepository.FindAsync(Arg.Any<Expression<Func<LessonOpening, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<LessonOpening>, IQueryable<LessonOpening>>?>(), Arg.Any<Func<IQueryable<LessonOpening>, IOrderedQueryable<LessonOpening>>?>(), Arg.Any<bool>())
            .Returns(call => _openings.Where(call.Arg<Expression<Func<LessonOpening, bool>>>().Compile()).ToList());
        _questionMasteryRepository.FindAsync(Arg.Any<Expression<Func<QuestionMastery, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<QuestionMastery>, IQueryable<QuestionMastery>>?>(), Arg.Any<Func<IQueryable<QuestionMastery>, IOrderedQueryable<QuestionMastery>>?>(), Arg.Any<bool>())
            .Returns(call => _masteries.Where(call.Arg<Expression<Func<QuestionMastery, bool>>>().Compile()).ToList());
        SubscriptionRepositoryStub.Stub(_subscriptionRepository);
        _handler = new StartUnitExamHandler(_sessionRepository, _unitRepository, _subjectRepository, _lessonRepository, _lessonOpeningRepository, _questionRepository, _examBlueprintRepository, _questionMasteryRepository, _subscriptionRepository, _examsOptions, Options.Create(new MasteryOptions()), Options.Create(new SubscriptionsOptions()), new Random(42), _timeProvider, _currentUserService, Substitute.For<ILocalizer>());
    }

    private CurriculumUnit Unit => _builder.Questions.Unit;

    [Fact]
    public async Task Handle_FreeStudentNewExam_ThrowsExamRequiresSubscription()
    {
        var act = () => _handler.Handle(new StartUnitExamCommand(Unit.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ExamRequiresSubscription);
        await _sessionRepository.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FreeStudentOpenExamForUnit_ResumesExam()
    {
        var open = Session.StartUnitExam(_builder.StudentId, Unit, _blueprints[0], _pool.Take(2).ToList(), [_builder.Questions.Lesson], false, ExamSessionBuilder.Now);
        SessionRepositoryStub.StubFind(_sessionRepository, open);

        var result = await _handler.Handle(new StartUnitExamCommand(Unit.Id), TestContext.Current.CancellationToken);

        result.Id.Should().Be(open.Id);
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AdminWithoutSubscription_StartsTestModeExam()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin));

        await _handler.Handle(new StartUnitExamCommand(Unit.Id), TestContext.Current.CancellationToken);

        await _sessionRepository.Received(1).AddAsync(Arg.Is<Session>(x => x.IsTestMode), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
