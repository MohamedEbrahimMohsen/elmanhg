using Elmanhg.Domain.Questions;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions;

public sealed class QuestionImportBatchTests
{
    private const string Hash = "ab12";
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _lessonId = Guid.NewGuid();
    private readonly Guid _creator = Guid.NewGuid();

    [Fact]
    public void Create_ValidArguments_SetsBatchIdLessonHashAndCount()
    {
        var batch = QuestionImportBatch.Create(_batchId, _lessonId, Hash, 3, _creator);

        batch.Id.Should().Be(_batchId);
        batch.LessonId.Should().Be(_lessonId);
        batch.FileHash.Should().Be(Hash);
        batch.QuestionCount.Should().Be(3);
        batch.CreatedBy.Should().Be(_creator);
    }

    [Fact]
    public void Matches_SameLessonAndHash_ReturnsTrue()
    {
        QuestionImportBatch.Create(_batchId, _lessonId, Hash, 3, _creator).Matches(_lessonId, Hash).Should().BeTrue();
    }

    [Fact]
    public void Matches_OtherHash_ReturnsFalse()
    {
        QuestionImportBatch.Create(_batchId, _lessonId, Hash, 3, _creator).Matches(_lessonId, "cd34").Should().BeFalse();
    }

    [Fact]
    public void Matches_OtherLesson_ReturnsFalse()
    {
        QuestionImportBatch.Create(_batchId, _lessonId, Hash, 3, _creator).Matches(Guid.NewGuid(), Hash).Should().BeFalse();
    }
}
