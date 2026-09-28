using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions;

public sealed class QuestionImportTests
{
    private readonly QuestionBuilder _builder = new();

    [Fact]
    public void CreateImported_ValidArguments_IsPendingVersionOneWithBatchId()
    {
        var batchId = Guid.NewGuid();

        var question = Question.CreateImported(_builder.Lesson, _builder.Unit, QuestionType.Mcq, QuestionBuilder.McqContent(), new QuestionMetadata(QuestionDifficulty.Easy, null, []), batchId, Guid.NewGuid());

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.Version.Should().Be(1);
        question.ImportBatchId.Should().Be(batchId);
        question.Revisions.Should().HaveCount(1);
    }
}
