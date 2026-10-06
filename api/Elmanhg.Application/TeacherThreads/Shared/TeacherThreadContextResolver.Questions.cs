using Core.DDD.Repositories;
using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.TeacherThreads.Shared;

public static partial class TeacherThreadContextResolver
{
    private sealed record QuestionPart(Question Question, int Version, string Stem, Guid? AttemptId);

    private static async Task<QuestionPart> ResolveAttemptAsync(Guid attemptId, Guid studentId, TeacherThreadContextSources sources, CancellationToken cancellationToken)
    {
        var attempt = await sources.Sessions.GetStudentAttemptAsync(attemptId, studentId, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.AttemptNotFound);
        var session = await sources.Sessions.GetRequiredAsync(attempt.SessionId, ErrorCodes.AttemptNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (session.IsExam && !session.IsSubmitted)
        {
            throw new ConflictCoreException(ErrorCodes.TeacherThreadExamInProgress);
        }

        var question = await sources.Questions.GetRequiredAsync(attempt.QuestionId, ErrorCodes.QuestionNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var revisions = await sources.Questions.GetRevisionsAsync([question.Id], cancellationToken).ConfigureAwait(false);
        var revision = revisions.FirstOrDefault(x => x.Version == attempt.QuestionVersion) ?? throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        return new(question, attempt.QuestionVersion, revision.ReadSnapshot().Stem, attempt.Id);
    }

    private static async Task<QuestionPart> ResolveQuestionAsync(Guid questionId, TeacherThreadContextSources sources, CancellationToken cancellationToken)
    {
        var question = await sources.Questions.GetRequiredAsync(questionId, ErrorCodes.QuestionNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return new(question, question.Version, question.Stem, null);
    }
}
