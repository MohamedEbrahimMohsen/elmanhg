using Core.Errors;
using Core.Utilities.Time;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.TeacherThreads;

public partial class TeacherThread
{
    // PRD §12.1 fixes the rating scale at 1–5; the web renders five stars.
    public const int MinRating = 1;
    public const int MaxRating = 5;

    public bool HasFollowUp() => Messages.Count(x => x.SenderId == StudentId) > 1;

    public bool CanFollowUp() => Status == TeacherThreadStatus.Answered;

    public bool CanBeRated() => Rating is null && Status != TeacherThreadStatus.Open;

    public TeacherMessage FollowUp(string text, DateTimeOffset askedAt, TeacherThreadSlaPolicy slaPolicy)
    {
        if (!CanFollowUp())
        {
            throw new ConflictCoreException(ErrorCodes.TeacherThreadFollowUpNotAllowed);
        }

        var at = askedAt.TruncateToMicroseconds();
        var message = TeacherMessage.CreateText(Id, StudentId, text, null, at);
        Messages.Add(message);
        Status = TeacherThreadStatus.Open;
        ApplySlaSchedule(slaPolicy.ScheduleFrom(at));
        UpdatedBy = StudentId;
        UpdationDate = at;
        return message;
    }

    public void Rate(int rating, DateTimeOffset ratedAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rating, MinRating);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rating, MaxRating);
        if (Status == TeacherThreadStatus.Open)
        {
            throw new ConflictCoreException(ErrorCodes.TeacherThreadNotAnswered);
        }

        if (Rating is not null)
        {
            throw new ConflictCoreException(ErrorCodes.TeacherThreadAlreadyRated);
        }

        var at = ratedAt.TruncateToMicroseconds();
        var wasClosed = Status == TeacherThreadStatus.Closed;
        Rating = rating;
        if (Status == TeacherThreadStatus.Answered)
        {
            Status = TeacherThreadStatus.Closed;
            ClosedAt = at;
        }

        UpdatedBy = StudentId;
        UpdationDate = at;
        RaiseDomainEvent(wasClosed ? new TeacherThreadRatedAfterClose(this, at) : new TeacherThreadClosed(this));
    }
}
