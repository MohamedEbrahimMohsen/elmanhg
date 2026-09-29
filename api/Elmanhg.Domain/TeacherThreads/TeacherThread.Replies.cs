using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.TeacherThreads;

public partial class TeacherThread
{
    public void Claim(Guid teacherId, DateTimeOffset claimedAt)
    {
        if (TeacherId == teacherId)
        {
            return;
        }

        if (TeacherId is not null)
        {
            throw new ConflictCoreException(ErrorCodes.TeacherThreadAlreadyClaimed);
        }

        var at = ToMicroseconds(claimedAt);
        TeacherId = teacherId;
        ClaimedAt = at;
        UpdatedBy = teacherId;
        UpdationDate = at;
    }

    public TeacherMessage Reply(Guid teacherId, string text, DateTimeOffset repliedAt)
    {
        EnsureClaimedBy(teacherId);
        if (Status != TeacherThreadStatus.Open)
        {
            throw new ConflictCoreException(ErrorCodes.TeacherThreadNotAwaitingReply);
        }

        var at = ToMicroseconds(repliedAt);
        var message = TeacherMessage.CreateText(Id, teacherId, text, null, at);
        Messages.Add(message);
        Status = TeacherThreadStatus.Answered;
        UpdatedBy = teacherId;
        UpdationDate = at;
        return message;
    }

    // Read receipts live on the message rows so a student opening the thread never changes the thread's xmin under a teacher's reply.
    public void MarkRepliesRead(DateTimeOffset readAt)
    {
        var at = ToMicroseconds(readAt);
        foreach (var message in Messages.Where(x => x.SenderId != StudentId))
        {
            message.MarkReadByStudent(at);
        }
    }

    private void EnsureClaimedBy(Guid teacherId)
    {
        if (TeacherId is null)
        {
            throw new ConflictCoreException(ErrorCodes.TeacherThreadNotClaimed);
        }

        if (TeacherId != teacherId)
        {
            throw new ConflictCoreException(ErrorCodes.TeacherThreadAlreadyClaimed);
        }
    }
}
