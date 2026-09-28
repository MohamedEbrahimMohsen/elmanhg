using Elmanhg.Application.QuestionValidation.GetValidationQueue;
using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.GetValidationQueue;

public sealed class ValidationQueueFilterTests
{
    private readonly QuestionBuilder _builder = new();

    [Fact]
    public void Build_PendingInAssignedSubject_Matches()
    {
        Matches(Query())(_builder.Build()).Should().BeTrue();
    }

    [Fact]
    public void Build_OtherSubject_Excludes()
    {
        Matches(Query())(new QuestionBuilder().Build()).Should().BeFalse();
    }

    [Fact]
    public void Build_Approved_Excludes()
    {
        Matches(Query())(_builder.Approved().Build()).Should().BeFalse();
    }

    [Fact]
    public void Build_Retired_Excludes()
    {
        Matches(Query())(_builder.Retired().Build()).Should().BeFalse();
    }

    [Fact]
    public void Build_UnitLessonIds_ExcludesOtherLessons()
    {
        var question = _builder.Build();

        Matches(Query(), unitLessonIds: [_builder.Lesson.Id])(question).Should().BeTrue();
        Matches(Query(), unitLessonIds: [Guid.NewGuid()])(question).Should().BeFalse();
    }

    [Fact]
    public void Build_LessonId_Matches()
    {
        var question = _builder.Build();

        Matches(Query() with { LessonId = _builder.Lesson.Id })(question).Should().BeTrue();
        Matches(Query() with { LessonId = Guid.NewGuid() })(question).Should().BeFalse();
    }

    [Fact]
    public void Build_Type_Excludes()
    {
        Matches(Query() with { Type = QuestionType.TrueFalse })(_builder.Build()).Should().BeFalse();
    }

    [Fact]
    public void Build_Difficulty_Excludes()
    {
        Matches(Query() with { Difficulty = QuestionDifficulty.Hard })(_builder.Build()).Should().BeFalse();
    }

    [Fact]
    public void Build_SubmittedBefore_ExcludesNewer()
    {
        var question = _builder.Build();

        Matches(Query(), submittedBefore: question.SubmittedAt.AddDays(-1))(question).Should().BeFalse();
        Matches(Query(), submittedBefore: question.SubmittedAt)(question).Should().BeTrue();
    }

    private static GetValidationQueueQuery Query() => new(null, null, null, null, null, null);

    private Func<Question, bool> Matches(GetValidationQueueQuery query, IReadOnlyCollection<Guid>? unitLessonIds = null, DateTimeOffset? submittedBefore = null)
    {
        return ValidationQueueFilter.Build([_builder.Subject.Id], unitLessonIds, query, submittedBefore).Compile();
    }
}
