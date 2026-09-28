using Elmanhg.Application.Questions.GetQuestions;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Questions.GetQuestions;

public sealed class GetQuestionsFilterTests
{
    private readonly QuestionBuilder _builder = new();

    [Fact]
    public void Build_NoFilters_MatchesEveryQuestion()
    {
        var matches = GetQuestionsFilter.Build(Query()).Compile();

        matches(_builder.Build()).Should().BeTrue();
        matches(new QuestionBuilder().Approved().Build()).Should().BeTrue();
        matches(new QuestionBuilder().Rejected("Wrong unit").Build()).Should().BeTrue();
    }

    [Fact]
    public void Build_Status_MatchesOnlyThatStatus()
    {
        var matches = GetQuestionsFilter.Build(Query() with { Status = QuestionValidationStatus.Rejected }).Compile();

        matches(new QuestionBuilder().Rejected("Wrong unit").Build()).Should().BeTrue();
        matches(_builder.Build()).Should().BeFalse();
        matches(new QuestionBuilder().Approved().Build()).Should().BeFalse();
    }

    [Fact]
    public void Build_Type_MatchesOnlyThatType()
    {
        var question = _builder.Build();

        GetQuestionsFilter.Build(Query() with { Type = QuestionType.Mcq }).Compile()(question).Should().BeTrue();
        GetQuestionsFilter.Build(Query() with { Type = QuestionType.TrueFalse }).Compile()(question).Should().BeFalse();
    }

    [Fact]
    public void Build_SubjectAndLesson_MatchOnlyThatScope()
    {
        var question = _builder.Build();

        GetQuestionsFilter.Build(Query() with { SubjectId = _builder.Subject.Id, LessonId = _builder.Lesson.Id }).Compile()(question).Should().BeTrue();
        GetQuestionsFilter.Build(Query() with { SubjectId = Guid.NewGuid() }).Compile()(question).Should().BeFalse();
        GetQuestionsFilter.Build(Query() with { LessonId = Guid.NewGuid() }).Compile()(question).Should().BeFalse();
    }

    [Fact]
    public void Build_TeacherId_MatchesQuestionsThatTeacherDecided()
    {
        var approved = _builder.Build();
        approved.Approve(TeacherSubject.Create(_builder.Teacher, _builder.Subject, Guid.NewGuid()), approved.Version);
        var rejected = _builder.Build();
        rejected.Reject(TeacherSubject.Create(_builder.Teacher, _builder.Subject, Guid.NewGuid()), rejected.Version, "Wrong unit");
        var matches = GetQuestionsFilter.Build(Query() with { TeacherId = _builder.Teacher.Id }).Compile();

        matches(approved).Should().BeTrue();
        matches(rejected).Should().BeTrue();
        matches(_builder.Build()).Should().BeFalse();
    }

    [Fact]
    public void Build_MinVersion_MatchesVersionAtLeast()
    {
        var edited = _builder.Build();
        edited.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, new QuestionMetadata(QuestionDifficulty.Medium, null, []), _builder.Lesson, Guid.NewGuid());
        var matches = GetQuestionsFilter.Build(Query() with { MinVersion = 2 }).Compile();

        matches(edited).Should().BeTrue();
        matches(new QuestionBuilder().Build()).Should().BeFalse();
    }

    [Fact]
    public void Build_RejectionReason_MatchesCaseInsensitiveContains()
    {
        var rejected = new QuestionBuilder().Rejected("Wrong UNIT").Build();

        GetQuestionsFilter.Build(Query() with { RejectionReason = "  unit " }).Compile()(rejected).Should().BeTrue();
        GetQuestionsFilter.Build(Query() with { RejectionReason = "time" }).Compile()(rejected).Should().BeFalse();
    }

    private static GetQuestionsQuery Query() => new(null, null, null, null, null, null, null);
}
