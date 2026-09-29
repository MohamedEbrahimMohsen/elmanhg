using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Tests.Builders;

public sealed class TeacherThreadBuilder
{
    public static readonly DateTimeOffset DefaultSubmittedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private Guid _studentId = Guid.NewGuid();
    private TeacherThreadContext _context = new(Guid.NewGuid(), "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", null, null, null, null);
    private DateTimeOffset _submittedAt = DefaultSubmittedAt;
    private string? _imageUrl;
    private Guid? _claimedBy;
    private bool _answered;
    private string? _voiceAudioUrl;
    private bool _followedUp;
    private bool _finalReplied;
    private int? _rating;

    public TeacherThreadBuilder ForStudent(Guid studentId)
    {
        _studentId = studentId;
        return this;
    }

    public TeacherThreadBuilder WithContext(TeacherThreadContext context)
    {
        _context = context;
        return this;
    }

    public TeacherThreadBuilder SubmittedAt(DateTimeOffset submittedAt)
    {
        _submittedAt = submittedAt;
        return this;
    }

    public TeacherThreadBuilder WithImage(string url)
    {
        _imageUrl = url;
        return this;
    }

    public TeacherThreadBuilder ClaimedBy(Guid teacherId)
    {
        _claimedBy = teacherId;
        return this;
    }

    public TeacherThreadBuilder AnsweredBy(Guid teacherId)
    {
        _claimedBy = teacherId;
        _answered = true;
        return this;
    }

    public TeacherThreadBuilder AnsweredByVoice(Guid teacherId, string audioUrl)
    {
        _claimedBy = teacherId;
        _voiceAudioUrl = audioUrl;
        return this;
    }

    public TeacherThreadBuilder FollowedUp()
    {
        _followedUp = true;
        return this;
    }

    public TeacherThreadBuilder FinalReplied()
    {
        _followedUp = true;
        _finalReplied = true;
        return this;
    }

    public TeacherThreadBuilder Rated(int rating)
    {
        _rating = rating;
        return this;
    }

    public TeacherThread Build()
    {
        var thread = TeacherThread.Submit(_studentId, _context, "Why is F = ma?", _imageUrl, _submittedAt, TimeSpan.FromHours(24));
        if (_claimedBy is { } teacherId)
        {
            thread.Claim(teacherId, _submittedAt.AddMinutes(10));
            if (_voiceAudioUrl is not null)
            {
                thread.ReplyWithVoice(teacherId, "Voice transcript.", _voiceAudioUrl, 42, _submittedAt.AddHours(1));
            }
            else if (_answered)
            {
                thread.Reply(teacherId, "Because force equals mass times acceleration.", _submittedAt.AddHours(1));
            }

            if (_followedUp)
            {
                thread.FollowUp("Can you show the units?", _submittedAt.AddHours(2), TimeSpan.FromHours(24));
            }

            if (_finalReplied)
            {
                thread.Reply(teacherId, "Newtons.", _submittedAt.AddHours(3));
            }
        }

        if (_rating is { } rating)
        {
            thread.Rate(rating, _submittedAt.AddHours(4));
        }

        return thread;
    }
}
