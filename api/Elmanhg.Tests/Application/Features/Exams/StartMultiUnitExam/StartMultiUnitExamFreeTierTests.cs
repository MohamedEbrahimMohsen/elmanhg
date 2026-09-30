using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exams.StartMultiUnitExam;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Domain.EssayGrading;
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

namespace Elmanhg.Tests.Application.Features.Exams.StartMultiUnitExam;

public sealed class StartMultiUnitExamFreeTierTests
{
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ILessonOpeningRepository _lessonOpeningRepository = Substitute.For<ILessonOpeningRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IExamBlueprintRepository _examBlueprintRepository = Substitute.For<IExamBlueprintRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly IEssayGradeRepository _essayGradeRepository = Substitute.For<IEssayGradeRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly MultiUnitExamBuilder _builder = new();
    private readonly List<Question> _pool = [];
    private readonly List<ExamBlueprint> _blueprints = [];
    private readonly List<LessonOpening> _openings = [];
    private readonly IOptions<ExamsOptions> _examsOptions = Options.Create(new ExamsOptions());
    private readonly StartMultiUnitExamHandler _handler;

    public StartMultiUnitExamFreeTierTests()
    {
        FillPool(10);
        _blueprints.AddRange([_builder.UnitBlueprint(0, 30, 50, new ExamTypeCount(QuestionType.Mcq, 10)), _builder.UnitBlueprint(1, 30, 50, new ExamTypeCount(QuestionType.Mcq, 10))]);
        _currentUserService.UserId.Returns(_builder.StudentId);
        _timeProvider.GetUtcNow().Returns(MultiUnitExamBuilder.Now.AddMinutes(1));
        _subjectRepository.GetByIdAsync(_builder.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_builder.Subject);
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => _builder.Units.Where(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()).ToList());
        _unitRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(call => _builder.Units.FirstOrDefault(x => x.Id == call.ArgAt<Guid>(0)));
        SessionRepositoryStub.StubFind(_sessionRepository);
        _examBlueprintRepository.FindAsync(Arg.Any<Expression<Func<ExamBlueprint, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IQueryable<ExamBlueprint>>?>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IOrderedQueryable<ExamBlueprint>>?>(), Arg.Any<bool>())
            .Returns(call => _blueprints.Where(call.Arg<Expression<Func<ExamBlueprint, bool>>>().Compile()).ToList());
        _questionRepository.GetServableExamCandidatesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => _pool.Where(x => call.Arg<IReadOnlyCollection<Guid>>().Contains(UnitOf(x))).Select(x => new ExamCandidate(x.Id, x.LessonId, x.Type, x.Difficulty)).ToList());
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => _pool.Where(call.Arg<Expression<Func<Question, bool>>>().Compile()).ToList());
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_ => _pool.SelectMany(x => x.Revisions).ToList());
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _builder.Lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _lessonOpeningRepository.FindAsync(Arg.Any<Expression<Func<LessonOpening, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<LessonOpening>, IQueryable<LessonOpening>>?>(), Arg.Any<Func<IQueryable<LessonOpening>, IOrderedQueryable<LessonOpening>>?>(), Arg.Any<bool>())
            .Returns(call => _openings.Where(call.Arg<Expression<Func<LessonOpening, bool>>>().Compile()).ToList());
        _questionMasteryRepository.FindAsync(Arg.Any<Expression<Func<QuestionMastery, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<QuestionMastery>, IQueryable<QuestionMastery>>?>(), Arg.Any<Func<IQueryable<QuestionMastery>, IOrderedQueryable<QuestionMastery>>?>(), Arg.Any<bool>())
            .Returns(new List<QuestionMastery>());
        SubscriptionRepositoryStub.Stub(_subscriptionRepository);
        _handler = new StartMultiUnitExamHandler(_sessionRepository, _subjectRepository, _unitRepository, _lessonRepository, _lessonOpeningRepository, _questionRepository, _examBlueprintRepository, _questionMasteryRepository, _essayGradeRepository, _subscriptionRepository, _examsOptions, Options.Create(new ExamBlueprintsOptions()), Options.Create(new MasteryOptions()), Options.Create(new SubscriptionsOptions()), new Random(42), _timeProvider, _currentUserService, Substitute.For<ILocalizer>(), Substitute.For<IAiMathCheckClient>(), Substitute.For<IFileStorage>());
    }

    [Fact]
    public async Task Handle_FreeStudentNewExam_ThrowsExamRequiresSubscription()
    {
        var act = () => _handler.Handle(Command(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ExamRequiresSubscription);
        await _sessionRepository.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FreeStudentOpenExamForSelection_ResumesExam()
    {
        var plan = MultiUnitBlueprintMerge.Merge(_blueprints.Select(x => new MultiUnitExamPart(x.UnitId.GetValueOrDefault(), x)).ToList(), 20, MultiUnitExamBuilder.MaxTimeLimitMinutes);
        var open = Session.StartMultiUnitExam(_builder.StudentId, _builder.Subject.Id, _builder.Units, plan, 20, _pool, _builder.Lessons, false, MultiUnitExamBuilder.Now);
        SessionRepositoryStub.StubFind(_sessionRepository, open);

        var result = await _handler.Handle(Command(), TestContext.Current.CancellationToken);

        result.Id.Should().Be(open.Id);
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AdminWithoutSubscription_StartsTestModeExam()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin));

        var result = await _handler.Handle(Command(), TestContext.Current.CancellationToken);

        result.IsTestMode.Should().BeTrue();
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private void FillPool(int perUnit) => _pool.AddRange(Enumerable.Range(0, 2).SelectMany(index => Enumerable.Range(0, perUnit).Select(_ => _builder.Approved(index))));

    private Guid UnitOf(Question question) => _builder.Lessons.Single(x => x.Id == question.LessonId).UnitId;

    private StartMultiUnitExamCommand Command() => new(new MultiUnitExamSelection(_builder.Subject.Id, _builder.Units.Select(x => x.Id).ToList(), 20));
}
