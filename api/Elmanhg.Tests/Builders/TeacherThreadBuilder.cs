using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Tests.Builders;

public sealed class TeacherThreadBuilder
{
    public static readonly DateTimeOffset DefaultSubmittedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private Guid _studentId = Guid.NewGuid();
    private TeacherThreadContext _context = new(Guid.NewGuid(), "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", null, null, null, null);
    private DateTimeOffset _submittedAt = DefaultSubmittedAt;
    private string? _imageUrl;

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

    public TeacherThread Build() => TeacherThread.Submit(_studentId, _context, "Why is F = ma?", _imageUrl, _submittedAt, TimeSpan.FromHours(24));
}
