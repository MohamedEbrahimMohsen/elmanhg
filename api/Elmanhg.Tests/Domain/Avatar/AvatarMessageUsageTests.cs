using Elmanhg.Domain.Avatar;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Avatar;

public sealed class AvatarMessageUsageTests
{
    [Fact]
    public void Record_ValidInput_SetsStudentEntryPointAndCreatedAt()
    {
        var studentId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

        var usage = AvatarMessageUsage.Record(studentId, AvatarEntryPoint.QuizQuestion, createdAt);

        usage.StudentId.Should().Be(studentId);
        usage.EntryPoint.Should().Be(AvatarEntryPoint.QuizQuestion);
        usage.CreatedAt.Should().Be(createdAt);
        usage.Id.Should().NotBeEmpty();
    }
}
