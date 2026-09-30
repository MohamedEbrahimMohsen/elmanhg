using Elmanhg.Application.Events.TrainingRecords;
using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Events;

public sealed class TeacherThreadTrainingRecordHandlerTests
{
    private const string StudentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly ITeacherThreadTrainingRecordRepository _teacherThreadTrainingRecordRepository = Substitute.For<ITeacherThreadTrainingRecordRepository>();
    private readonly IStudentIdHasher _studentIdHasher = Substitute.For<IStudentIdHasher>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly TeacherThreadTrainingRecordHandler _handler;
    private TeacherThreadTrainingRecord? _added;

    public TeacherThreadTrainingRecordHandlerTests()
    {
        _studentIdHasher.Hash(Arg.Any<Guid>()).Returns(StudentHash);
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherThreadTrainingRecordRepository.AddAsync(Arg.Do<TeacherThreadTrainingRecord>(x => _added = x), Arg.Any<CancellationToken>());
        _handler = new TeacherThreadTrainingRecordHandler(_teacherThreadTrainingRecordRepository, _studentIdHasher, _timeProvider);
    }

    [Fact]
    public async Task Handle_TeacherThreadClosed_AddsClosedRecordAtClosedAt()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(Guid.NewGuid()).FinalReplied().Build();

        await _handler.Handle(new TeacherThreadClosed(thread), TestContext.Current.CancellationToken);

        _added.Should().NotBeNull();
        (_added!.Trigger, _added.OccurredAt, _added.StudentHash, _added.RecordedAt).Should().Be((TeacherThreadTrainingTrigger.Closed, thread.ClosedAt!.Value, StudentHash, Now));
        _studentIdHasher.Received(1).Hash(thread.StudentId);
        await _teacherThreadTrainingRecordRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RatedAfterClose_AddsRatedRecordAtRatedAt()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(Guid.NewGuid()).FinalReplied().Rated(4).Build();
        var ratedAt = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(4);

        await _handler.Handle(new TeacherThreadRatedAfterClose(thread, ratedAt), TestContext.Current.CancellationToken);

        _added.Should().NotBeNull();
        (_added!.Trigger, _added.OccurredAt, _added.Rating).Should().Be((TeacherThreadTrainingTrigger.RatedAfterClose, ratedAt, (int?)4));
        await _teacherThreadTrainingRecordRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
