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

    public void EnsureCanReply(Guid teacherId)
    {
        EnsureClaimedBy(teacherId);
        if (Status != TeacherThreadStatus.Open)
        {
            throw new ConflictCoreException(ErrorCodes.TeacherThreadNotAwaitingReply);
        }
    }

    public TeacherMessage Reply(Guid teacherId, string text, DateTimeOffset repliedAt)
    {
        EnsureCanReply(teacherId);
        var at = ToMicroseconds(repliedAt);
        return Answer(teacherId, TeacherMessage.CreateText(Id, teacherId, text, null, at), at);
    }

    public TeacherMessage ReplyWithVoice(Guid teacherId, string text, string audioUrl, int audioDurationSeconds, DateTimeOffset repliedAt)
    {
        EnsureCanReply(teacherId);
        var at = ToMicroseconds(repliedAt);
        return Answer(teacherId, TeacherMessage.CreateVoice(Id, teacherId, text, audioUrl, audioDurationSeconds, at), at);
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

    private TeacherMessage Answer(Guid teacherId, TeacherMessage message, DateTimeOffset at)
    {
        Messages.Add(message);
        Status = TeacherThreadStatus.Answered;
        UpdatedBy = teacherId;
        UpdationDate = at;
        return message;
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
