using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Avatar.Shared;

// sessions.md "What is revealed": a question's explanation stays hidden while it is an unanswered item of an open quiz or an item of an unsubmitted exam, so its chunks are not sent as sources either.
public static class AvatarWithheldQuestions
{
    public static async Task<HashSet<Guid>> LoadAsync(Guid studentId, ISessionRepository sessionRepository, CancellationToken cancellationToken)
    {
        var sessions = await sessionRepository.FindAsync(x => x.StudentId == studentId && x.SubmittedAt == null, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery(), asNoTracking: true).ConfigureAwait(false);
        return sessions
            .SelectMany(session => session.Items.Where(item => session.IsExam || session.FindAttempt(item.QuestionId) is null))
            .Select(x => x.QuestionId)
            .ToHashSet();
    }

    public static List<LessonContentMatchResult> Filter(IReadOnlyList<LessonContentMatchResult> matches, IReadOnlySet<Guid> withheld) => matches
        .Where(x => x.QuestionId is not { } questionId || !withheld.Contains(questionId))
        .ToList();
}
