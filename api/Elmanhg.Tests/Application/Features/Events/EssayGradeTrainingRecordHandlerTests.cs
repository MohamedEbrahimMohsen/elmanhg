using Elmanhg.Application.Events.TrainingRecords;
using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Events;

public sealed class EssayGradeTrainingRecordHandlerTests
{
    private const string StudentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly IEssayGradeTrainingRecordRepository _essayGradeTrainingRecordRepository = Substitute.For<IEssayGradeTrainingRecordRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IStudentIdHasher _studentIdHasher = Substitute.For<IStudentIdHasher>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly EssayGradeTrainingRecordHandler _handler;
    private readonly Guid _studentId = Guid.NewGuid();
    private EssayGradeTrainingRecord? _added;

    public EssayGradeTrainingRecordHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _studentIdHasher.Hash(_studentId).Returns(StudentHash);
        _essayGradeTrainingRecordRepository.AddAsync(Arg.Do<EssayGradeTrainingRecord>(x => _added = x), Arg.Any<CancellationToken>());
        _handler = new EssayGradeTrainingRecordHandler(_essayGradeTrainingRecordRepository, _sessionRepository, _questionRepository, _studentIdHasher, _timeProvider);
    }

    [Fact]
    public async Task Handle_StudentSession_AddsRecordWithHashAndPlacement()
    {
        var grade = CompletedGrade();
        GivenSession(isTestMode: false);
        var placement = GivenPlacement(grade.QuestionId);

        await _handler.Handle(new EssayGradeCompleted(grade), TestContext.Current.CancellationToken);

        _added.Should().NotBeNull();
        (_added!.StudentHash, _added.EssayGradeId, _added.SessionKind, _added.RecordedAt).Should().Be((StudentHash, grade.Id, SessionKind.Quiz, Now));
        (_added.SubjectId, _added.UnitId, _added.LessonId).Should().Be((placement.SubjectId, placement.UnitId, placement.LessonId));
        await _essayGradeTrainingRecordRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TestModeSession_AddsNothing()
    {
        var grade = CompletedGrade();
        GivenSession(isTestMode: true);

        await _handler.Handle(new EssayGradeCompleted(grade), TestContext.Current.CancellationToken);

        await _questionRepository.DidNotReceive().GetPlacementsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await _essayGradeTrainingRecordRepository.DidNotReceive().AddAsync(Arg.Any<EssayGradeTrainingRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MissingSession_ThrowsInvalidOperationException()
    {
        var grade = CompletedGrade();

        var act = () => _handler.Handle(new EssayGradeCompleted(grade), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _essayGradeTrainingRecordRepository.DidNotReceive().AddAsync(Arg.Any<EssayGradeTrainingRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MissingPlacement_ThrowsInvalidOperationException()
    {
        var grade = CompletedGrade();
        GivenSession(isTestMode: false);
        _questionRepository.GetPlacementsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, QuestionPlacement>());

        var act = () => _handler.Handle(new EssayGradeCompleted(grade), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _essayGradeTrainingRecordRepository.DidNotReceive().AddAsync(Arg.Any<EssayGradeTrainingRecord>(), Arg.Any<CancellationToken>());
    }

    private EssayGrade CompletedGrade()
    {
        var grade = new EssayGradeBuilder().ForStudent(_studentId).Build();
        grade.Complete(EssayGradeBuilder.Assessment(0.9m), new QuestionGrade(2.5m, 0.5m, GradeOutcome.Partial, null), 0.7m, EssayGradeBuilder.DefaultRequestedAt.AddSeconds(40));
        return grade;
    }

    private void GivenSession(bool isTestMode)
    {
        var session = new SessionBuilder().Build(isTestMode: isTestMode);
        _sessionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), true).Returns(session);
    }

    private QuestionPlacement GivenPlacement(Guid questionId)
    {
        var placement = new QuestionPlacement(questionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _questionRepository.GetPlacementsAsync(Arg.Is<IReadOnlyCollection<Guid>>(x => x.Single() == questionId), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, QuestionPlacement> { [questionId] = placement });
        return placement;
    }
}
