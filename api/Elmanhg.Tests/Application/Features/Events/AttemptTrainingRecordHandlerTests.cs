using Elmanhg.Application.Events.TrainingRecords;
using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Events;

public sealed class AttemptTrainingRecordHandlerTests
{
    private const string StudentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly IAttemptTrainingRecordRepository _attemptTrainingRecordRepository = Substitute.For<IAttemptTrainingRecordRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IStudentIdHasher _studentIdHasher = Substitute.For<IStudentIdHasher>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly AttemptTrainingRecordHandler _handler;
    private List<AttemptTrainingRecord>? _added;

    public AttemptTrainingRecordHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _studentIdHasher.Hash(Arg.Any<Guid>()).Returns(StudentHash);
        _attemptTrainingRecordRepository.AddRangeAsync(Arg.Do<List<AttemptTrainingRecord>>(x => _added = x), Arg.Any<CancellationToken>());
        _handler = new AttemptTrainingRecordHandler(_attemptTrainingRecordRepository, _questionRepository, _studentIdHasher, _timeProvider);
    }

    [Fact]
    public async Task Handle_StudentQuizAttempt_AddsRecordWithHashAndPlacement()
    {
        var builder = new SessionBuilder();
        var session = builder.Build();
        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        var placement = PlacementOf(attempt.QuestionId);
        GivenPlacements(placement);

        await _handler.Handle(new AttemptsRecorded(session, [attempt]), TestContext.Current.CancellationToken);

        var record = _added.Should().ContainSingle().Subject;
        (record.StudentHash, record.AttemptId, record.SessionKind, record.RecordedAt).Should().Be((StudentHash, attempt.Id, SessionKind.Quiz, Now));
        (record.SubjectId, record.UnitId, record.LessonId).Should().Be((placement.SubjectId, placement.UnitId, placement.LessonId));
        _studentIdHasher.Received(1).Hash(builder.StudentId);
        await _attemptTrainingRecordRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TestModeSession_AddsNothing()
    {
        var session = new SessionBuilder().Build(isTestMode: true);
        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        await _handler.Handle(new AttemptsRecorded(session, [attempt]), TestContext.Current.CancellationToken);

        await _questionRepository.DidNotReceive().GetPlacementsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await _attemptTrainingRecordRepository.DidNotReceive().AddRangeAsync(Arg.Any<List<AttemptTrainingRecord>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExamAttempts_LoadsPlacementsOnceAndAddsOneRecordPerAttempt()
    {
        var session = new ExamSessionBuilder().Build(count: 2);
        session.SaveExamAnswer(session.Items[0], SessionBuilder.AnswerB, TimeSpan.FromSeconds(30), ExamSessionBuilder.Now.AddMinutes(1));
        session.SaveExamAnswer(session.Items[1], SessionBuilder.AnswerA, TimeSpan.FromSeconds(30), ExamSessionBuilder.Now.AddMinutes(2));
        var attempts = session.SubmitExam(session.Items.ToDictionary(x => x.QuestionId, _ => SessionBuilder.Grade(1m)), ExamSessionBuilder.Now.AddMinutes(10));
        GivenPlacements([.. attempts.Select(x => PlacementOf(x.QuestionId))]);

        await _handler.Handle(new AttemptsRecorded(session, attempts), TestContext.Current.CancellationToken);

        await _questionRepository.Received(1).GetPlacementsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        _added.Should().HaveCount(2).And.OnlyContain(x => x.SessionKind == SessionKind.UnitExam);
        _added!.Select(x => x.AttemptId).Should().Equal(attempts.Select(x => x.Id));
    }

    [Fact]
    public async Task Handle_MissingPlacement_ThrowsInvalidOperationException()
    {
        var session = new SessionBuilder().Build();
        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        GivenPlacements();

        var act = () => _handler.Handle(new AttemptsRecorded(session, [attempt]), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _attemptTrainingRecordRepository.DidNotReceive().AddRangeAsync(Arg.Any<List<AttemptTrainingRecord>>(), Arg.Any<CancellationToken>());
    }

    private static QuestionPlacement PlacementOf(Guid questionId) => new(questionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private void GivenPlacements(params QuestionPlacement[] placements)
    {
        _questionRepository.GetPlacementsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(placements.ToDictionary(x => x.QuestionId));
    }
}
