using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.TrainingData;
using MediatR;

namespace Elmanhg.Application.Events.TrainingRecords;

public sealed class AttemptTrainingRecordHandler(IAttemptTrainingRecordRepository attemptTrainingRecordRepository, IQuestionRepository questionRepository, IStudentIdHasher studentIdHasher, TimeProvider timeProvider) : INotificationHandler<AttemptsRecorded>
{
    public async Task Handle(AttemptsRecorded notification, CancellationToken cancellationToken)
    {
        if (notification.Session.IsTestMode)
        {
            return;
        }

        var questionIds = notification.Attempts.Select(x => x.QuestionId).Distinct().ToList();
        var placements = await questionRepository.GetPlacementsAsync(questionIds, cancellationToken).ConfigureAwait(false);
        var studentHash = studentIdHasher.Hash(notification.Session.StudentId);
        var now = timeProvider.GetUtcNow();
        var records = notification.Attempts
            .Select(x => AttemptTrainingRecord.From(x, notification.Session.Kind, placements.GetValueOrDefault(x.QuestionId) ?? throw new InvalidOperationException($"Question {x.QuestionId} has no placement."), studentHash, now))
            .ToList();
        await attemptTrainingRecordRepository.AddRangeAsync(records, cancellationToken).ConfigureAwait(false);
    }
}
