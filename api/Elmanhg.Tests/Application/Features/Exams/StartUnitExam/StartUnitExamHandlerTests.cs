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
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Exams.StartUnitExam;

public sealed class StartUnitExamHandlerTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IExamBlueprintRepository _examBlueprintRepository = Substitute.For<IExamBlueprintRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ExamSessionBuilder _builder = new();
    private readonly List<Question> _pool;
    private readonly List<ExamBlueprint> _blueprints = [];
    private readonly List<QuestionMastery> _masteries = [];
    private readonly StartUnitExamHandler _handler;

    public StartUnitExamHandlerTests()
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
        _questionMasteryRepository.FindAsync(Arg.Any<Expression<Func<QuestionMastery, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<QuestionMastery>, IQueryable<QuestionMastery>>?>(), Arg.Any<Func<IQueryable<QuestionMastery>, IOrderedQueryable<QuestionMastery>>?>(), Arg.Any<bool>())
            .Returns(call => _masteries.Where(call.Arg<Expression<Func<QuestionMastery, bool>>>().Compile()).ToList());
        _handler = new StartUnitExamHandler(_sessionRepository, _unitRepository, _subjectRepository, _lessonRepository, _questionRepository, _examBlueprintRepository, _questionMasteryRepository, Options.Create(new ExamsOptions()), Options.Create(new MasteryOptions()), new Random(42), _timeProvider, _currentUserService, Substitute.For<ILocalizer>());
    }

    private CurriculumUnit Unit => _builder.Questions.Unit;

    [Fact]
    public async Task Handle_NoUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await AssertThrowsAsync<UnauthorizedCoreException>(Unit.Id, ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownUnit_ThrowsNotFound()
    {
        await AssertThrowsAsync<NotFoundCoreException>(Guid.NewGuid(), ErrorCodes.UnitNotFound);
    }

    [Fact]
    public async Task Handle_OpenExamOtherUnit_ThrowsExamAlreadyInProgress()
    {
        var other = new ExamSessionBuilder();
        SessionRepositoryStub.StubFind(_sessionRepository, Session.StartUnitExam(_builder.StudentId, other.Questions.Unit, other.Blueprint(1), other.BuildQuestions(1), [other.Questions.Lesson], false, ExamSessionBuilder.Now));

        await AssertThrowsAsync<ConflictCoreException>(Unit.Id, ErrorCodes.ExamAlreadyInProgress);
    }

    [Fact]
    public async Task Handle_OpenExamThisUnit_ResumesWithoutNewSession()
    {
        var open = OpenExam();

        var result = await _handler.Handle(new StartUnitExamCommand(Unit.Id), TestContext.Current.CancellationToken);

        result.Id.Should().Be(open.Id);
        result.SubmittedAt.Should().BeNull();
        await _sessionRepository.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExpiredOpenExamThisUnit_SubmitsAndReturnsIt()
    {
        var open = OpenExam();
        open.SaveExamAnswer(open.Items[0], SessionBuilder.AnswerB, Grace, ExamSessionBuilder.Now.AddMinutes(1));
        _timeProvider.GetUtcNow().Returns(ExamSessionBuilder.Now.AddMinutes(40));

        var result = await _handler.Handle(new StartUnitExamCommand(Unit.Id), TestContext.Current.CancellationToken);

        result.Id.Should().Be(open.Id);
        result.SubmittedAt.Should().NotBeNull();
        open.Attempts.Should().ContainSingle().Which.QuestionId.Should().Be(open.Items[0].QuestionId);
        await _sessionRepository.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoBlueprint_ThrowsBadRequest()
    {
        _blueprints.Clear();

        await AssertThrowsAsync<BadRequestCoreException>(Unit.Id, ErrorCodes.UnitExamNoBlueprint);
    }

    [Fact]
    public async Task Handle_Shortfall_ThrowsExamShortfall()
    {
        _blueprints[0] = _builder.Blueprint(5);

        await AssertThrowsAsync<BusinessRuleViolationCoreException>(Unit.Id, DomainErrorCodes.ExamShortfall);
    }

    [Fact]
    public async Task Handle_ServableUnit_StartsExamFromBlueprint()
    {
        var result = await _handler.Handle(new StartUnitExamCommand(Unit.Id), TestContext.Current.CancellationToken);

        await _sessionRepository.Received(1).AddAsync(Arg.Is<Session>(x => x.Id == result.Id && x.Items.Count == 2 && x.PassMark == 50 && x.Deadline == x.StartedAt.AddMinutes(30) && x.Kind == SessionKind.UnitExam), Arg.Any<CancellationToken>());
        result.Items.Should().HaveCount(2).And.AllSatisfy(x => x.CorrectAnswer.Should().BeNull());
        result.ServerNow.Should().Be(ExamSessionBuilder.Now.AddMinutes(1));
        result.Units.Should().ContainSingle().Which.UnitId.Should().Be(Unit.Id);
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MasteredQuestions_PrefersNotMastered()
    {
        var mastery = QuestionMastery.Start(_builder.StudentId, _pool[0].Id, new MasteryAttempt(Guid.NewGuid(), 1m, ExamSessionBuilder.Now.AddDays(-2)));
        mastery.Record(new MasteryAttempt(Guid.NewGuid(), 1m, ExamSessionBuilder.Now.AddDays(-1)), 0.8m);
        _masteries.Add(mastery);

        var result = await _handler.Handle(new StartUnitExamCommand(Unit.Id), TestContext.Current.CancellationToken);

        result.Items.Select(x => x.QuestionId).Should().HaveCount(2).And.NotContain(_pool[0].Id);
    }

    [Fact]
    public async Task Handle_Admin_StartsTestModeExam()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin));

        var result = await _handler.Handle(new StartUnitExamCommand(Unit.Id), TestContext.Current.CancellationToken);

        result.IsTestMode.Should().BeTrue();
        await _sessionRepository.Received(1).AddAsync(Arg.Is<Session>(x => x.IsTestMode), Arg.Any<CancellationToken>());
    }

    private Session OpenExam()
    {
        var open = Session.StartUnitExam(_builder.StudentId, Unit, _blueprints[0], _pool.Take(2).ToList(), [_builder.Questions.Lesson], false, ExamSessionBuilder.Now);
        SessionRepositoryStub.StubFind(_sessionRepository, open);
        return open;
    }

    private async Task AssertThrowsAsync<TException>(Guid unitId, string errorCode) where TException : BaseException
    {
        var act = () => _handler.Handle(new StartUnitExamCommand(unitId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
        await _sessionRepository.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
