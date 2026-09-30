using Elmanhg.Application.Events.TrainingRecords;
using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Events;

public sealed class AvatarTrainingRecordHandlerTests
{
    private const string StudentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly IAvatarTrainingRecordRepository _avatarTrainingRecordRepository = Substitute.For<IAvatarTrainingRecordRepository>();
    private readonly IStudentIdHasher _studentIdHasher = Substitute.For<IStudentIdHasher>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private AvatarTrainingRecord? _added;

    [Fact]
    public async Task Handle_Exchange_AddsRecordWithHashedStudent()
    {
        var studentId = Guid.NewGuid();
        var conversation = new AvatarConversationBuilder().ForStudent(studentId).WithExchange("question", "reply").Build();
        _studentIdHasher.Hash(studentId).Returns(StudentHash);
        _timeProvider.GetUtcNow().Returns(Now);
        await _avatarTrainingRecordRepository.AddAsync(Arg.Do<AvatarTrainingRecord>(x => _added = x), Arg.Any<CancellationToken>());
        var handler = new AvatarTrainingRecordHandler(_avatarTrainingRecordRepository, _studentIdHasher, _timeProvider);

        await handler.Handle(new AvatarExchangeRecorded(conversation, conversation.Messages[0], conversation.Messages[1]), TestContext.Current.CancellationToken);

        _added.Should().NotBeNull();
        (_added!.StudentHash, _added.StudentMessageId, _added.RecordedAt).Should().Be((StudentHash, conversation.Messages[0].Id, Now));
        await _avatarTrainingRecordRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
