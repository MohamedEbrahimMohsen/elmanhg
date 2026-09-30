using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.TrainingData;
using MediatR;

namespace Elmanhg.Application.Events.TrainingRecords;

public sealed class EssayGradeTrainingRecordHandler(IEssayGradeTrainingRecordRepository essayGradeTrainingRecordRepository, ISessionRepository sessionRepository, IQuestionRepository questionRepository, IStudentIdHasher studentIdHasher, TimeProvider timeProvider) : INotificationHandler<EssayGradeCompleted>
{
    public async Task Handle(EssayGradeCompleted notification, CancellationToken cancellationToken)
    {
        var grade = notification.Grade;
        var session = await sessionRepository.GetByIdAsync(grade.SessionId, cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? throw new InvalidOperationException($"Session {grade.SessionId} of essay grade {grade.Id} was not found.");
        if (session.IsTestMode)
        {
            return;
        }

        var placements = await questionRepository.GetPlacementsAsync([grade.QuestionId], cancellationToken).ConfigureAwait(false);
        var placement = placements.GetValueOrDefault(grade.QuestionId) ?? throw new InvalidOperationException($"Question {grade.QuestionId} has no placement.");
        var record = EssayGradeTrainingRecord.From(grade, session.Kind, placement, studentIdHasher.Hash(grade.StudentId), timeProvider.GetUtcNow());
        await essayGradeTrainingRecordRepository.AddAsync(record, cancellationToken).ConfigureAwait(false);
    }
}
