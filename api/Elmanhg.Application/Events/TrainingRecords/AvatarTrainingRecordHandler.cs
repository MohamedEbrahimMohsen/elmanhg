using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.TrainingData;
using MediatR;

namespace Elmanhg.Application.Events.TrainingRecords;

public sealed class AvatarTrainingRecordHandler(IAvatarTrainingRecordRepository avatarTrainingRecordRepository, IStudentIdHasher studentIdHasher, TimeProvider timeProvider) : INotificationHandler<AvatarExchangeRecorded>
{
    public async Task Handle(AvatarExchangeRecorded notification, CancellationToken cancellationToken)
    {
        var record = AvatarTrainingRecord.From(notification.Conversation, notification.StudentMessage, notification.AssistantMessage, studentIdHasher.Hash(notification.Conversation.StudentId), timeProvider.GetUtcNow());
        await avatarTrainingRecordRepository.AddAsync(record, cancellationToken).ConfigureAwait(false);
    }
}
