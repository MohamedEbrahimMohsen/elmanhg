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

public sealed class EssayGradeReviewTrainingRecordHandlerTests
{
    private const string StudentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly IEssayGradeTrainingRecordRepository _essayGradeTrainingRecordRepository = Substitute.For<IEssayGradeTrainingRecordRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IStudentIdHasher _studentIdHasher = Substitute.For<IStudentIdHasher>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly EssayGradeReviewTrainingRecordHandler _handler;
    private readonly Guid _studentId = Guid.NewGuid();
    private EssayGradeTrainingRecord? _added;

    public EssayGradeReviewTrainingRecordHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _studentIdHasher.Hash(_studentId).Returns(StudentHash);
        _essayGradeTrainingRecordRepository.AddAsync(Arg.Do<EssayGradeTrainingRecord>(x => _added = x), Arg.Any<CancellationToken>());
        _handler = new EssayGradeReviewTrainingRecordHandler(_essayGradeTrainingRecordRepository, _sessionRepository, _questionRepository, _studentIdHasher, _timeProvider);
    }

    [Fact]
    public async Task Handle_ReviewedAiGrade_AddsTeacherReviewedRecord()
    {
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder().ForStudent(_studentId));
        grade.Override(4m, Guid.NewGuid(), "Full marks for the definition.", Now.AddMinutes(-1));
        GivenSession(isTestMode: false);
        GivenPlacement(grade.QuestionId);

        await _handler.Handle(new EssayGradeReviewed(grade), TestContext.Current.CancellationToken);

        _added.Should().NotBeNull();
        (_added!.Trigger, _added.StudentHash, _added.EssayGradeId, _added.ReviewedScore, _added.RecordedAt).Should().Be((EssayGradeTrainingTrigger.TeacherReviewed, StudentHash, grade.Id, (decimal?)4m, Now));
    }

    [Fact]
    public async Task Handle_GradingFailedReview_AddsNothing()
    {
        var grade = EssayGradeBuilder.GradingFailed(new EssayGradeBuilder().ForStudent(_studentId));
        grade.Override(3m, Guid.NewGuid(), "Graded by hand.", Now.AddMinutes(-1));
        GivenSession(isTestMode: false);
        GivenPlacement(grade.QuestionId);

        await _handler.Handle(new EssayGradeReviewed(grade), TestContext.Current.CancellationToken);

        await _essayGradeTrainingRecordRepository.DidNotReceive().AddAsync(Arg.Any<EssayGradeTrainingRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TestModeSession_AddsNothing()
    {
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder().ForStudent(_studentId));
        grade.Accept(Guid.NewGuid(), null, Now.AddMinutes(-1));
        GivenSession(isTestMode: true);
        GivenPlacement(grade.QuestionId);

        await _handler.Handle(new EssayGradeReviewed(grade), TestContext.Current.CancellationToken);

        await _essayGradeTrainingRecordRepository.DidNotReceive().AddAsync(Arg.Any<EssayGradeTrainingRecord>(), Arg.Any<CancellationToken>());
    }

    private void GivenSession(bool isTestMode)
    {
        var session = new SessionBuilder().Build(isTestMode: isTestMode);
        _sessionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), true).Returns(session);
    }

    private void GivenPlacement(Guid questionId)
    {
        var placement = new QuestionPlacement(questionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _questionRepository.GetPlacementsAsync(Arg.Is<IReadOnlyCollection<Guid>>(x => x.Single() == questionId), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, QuestionPlacement> { [questionId] = placement });
    }
}
