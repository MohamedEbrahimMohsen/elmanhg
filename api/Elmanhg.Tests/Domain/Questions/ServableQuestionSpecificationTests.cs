using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions;

public sealed class ServableQuestionSpecificationTests
{
    private readonly QuestionBuilder _builder = new();

    [Fact]
    public void IsSatisfiedBy_ApprovedInPublishedLesson_ReturnsTrue()
    {
        _builder.Lesson.Publish(Guid.NewGuid());
        var question = _builder.Approved().Build();

        ServableQuestionSpecification.IsSatisfiedBy(question, _builder.Lesson).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_PendingQuestion_ReturnsFalse()
    {
        _builder.Lesson.Publish(Guid.NewGuid());
        var question = _builder.Build();

        ServableQuestionSpecification.IsSatisfiedBy(question, _builder.Lesson).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_RejectedQuestion_ReturnsFalse()
    {
        _builder.Lesson.Publish(Guid.NewGuid());
        var question = _builder.Rejected("x").Build();

        ServableQuestionSpecification.IsSatisfiedBy(question, _builder.Lesson).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_RetiredApprovedQuestion_ReturnsFalse()
    {
        _builder.Lesson.Publish(Guid.NewGuid());
        var question = _builder.Approved().Retired().Build();

        ServableQuestionSpecification.IsSatisfiedBy(question, _builder.Lesson).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_DraftLesson_ReturnsFalse()
    {
        var question = _builder.Approved().Build();

        ServableQuestionSpecification.IsSatisfiedBy(question, _builder.Lesson).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_ArchivedLesson_ReturnsFalse()
    {
        _builder.Lesson.Publish(Guid.NewGuid());
        _builder.Lesson.Archive(Guid.NewGuid());
        var question = _builder.Approved().Build();

        ServableQuestionSpecification.IsSatisfiedBy(question, _builder.Lesson).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_LessonOfAnotherQuestion_ReturnsFalse()
    {
        var question = _builder.Approved().Build();
        var otherLesson = Lesson.Create(_builder.Unit, "Energy", 2, Guid.NewGuid());
        otherLesson.Publish(Guid.NewGuid());

        ServableQuestionSpecification.IsSatisfiedBy(question, otherLesson).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_ApprovedThenContentEdited_ReturnsFalse()
    {
        _builder.Lesson.Publish(Guid.NewGuid());
        var question = _builder.Approved().Build();

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, new QuestionMetadata(QuestionDifficulty.Medium, null, []), _builder.Lesson, Guid.NewGuid());

        ServableQuestionSpecification.IsSatisfiedBy(question, _builder.Lesson).Should().BeFalse();
    }

    [Fact]
    public void WhereServable_MixedQuestionsAndLessons_KeepsOnlyServable()
    {
        _builder.Lesson.Publish(Guid.NewGuid());
        var pending = _builder.Build();
        var servable = _builder.Approved().Build();
        var retired = _builder.Retired().Build();
        var draftBuilder = new QuestionBuilder();
        var inDraftLesson = draftBuilder.Approved().Build();
        List<Question> questions = [servable, pending, retired, inDraftLesson];
        List<Lesson> lessons = [_builder.Lesson, draftBuilder.Lesson];

        var result = questions
            .AsQueryable()
            .WhereServable(lessons.AsQueryable())
            .Select(x => x.Id)
            .ToList();

        result.Should().Equal(servable.Id);
    }

    [Fact]
    public void IsSatisfiedBy_ApprovedEssayInPublishedLesson_ReturnsTrue()
    {
        _builder.Lesson.Publish(Guid.NewGuid());
        var question = _builder.Essay().Approved().Build();

        ServableQuestionSpecification.IsSatisfiedBy(question, _builder.Lesson).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_ApprovedMathStepsInPublishedLesson_ReturnsTrue()
    {
        _builder.Lesson.Publish(Guid.NewGuid());
        var question = _builder.MathSteps().Approved().Build();

        ServableQuestionSpecification.IsSatisfiedBy(question, _builder.Lesson).Should().BeTrue();
    }

    [Fact]
    public void ServedTypes_EveryType()
    {
        ServableQuestionSpecification.ServedTypes.Should().Equal(Enum.GetValues<QuestionType>());
    }
}
