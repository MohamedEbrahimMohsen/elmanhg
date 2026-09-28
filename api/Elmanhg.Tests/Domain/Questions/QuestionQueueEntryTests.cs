using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions;

public sealed class QuestionQueueEntryTests
{
    private readonly QuestionBuilder _builder = new();
    private readonly Guid _editor = Guid.NewGuid();

    [Fact]
    public void Create_SetsSubmittedAtToNow()
    {
        var question = _builder.Build();

        question.SubmittedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Update_MetadataOnly_KeepsSubmittedAt()
    {
        var question = _builder.Build();
        var before = question.SubmittedAt;

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent(), new QuestionMetadata(QuestionDifficulty.Hard, null, ["forces"]), _builder.Lesson, _editor);

        question.SubmittedAt.Should().Be(before);
        question.Version.Should().Be(1);
    }

    [Fact]
    public void Update_ContentChange_ResetsSubmittedAt()
    {
        var question = _builder.Build();
        var before = question.SubmittedAt;

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, Metadata(), _builder.Lesson, _editor);

        question.SubmittedAt.Should().BeOnOrAfter(before);
        question.SubmittedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        question.Version.Should().Be(2);
    }

    [Fact]
    public void Resubmit_Rejected_ResetsSubmittedAt()
    {
        var question = _builder.Rejected("Wrong unit").Build();
        var before = question.SubmittedAt;

        question.Resubmit(QuestionType.Mcq, QuestionBuilder.McqContent(), Metadata(), _builder.Lesson, _editor);

        question.SubmittedAt.Should().BeOnOrAfter(before);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    private static QuestionMetadata Metadata()
    {
        return new QuestionMetadata(QuestionDifficulty.Medium, null, []);
    }
}
