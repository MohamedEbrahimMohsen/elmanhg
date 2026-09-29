using Elmanhg.Application.LoadTesting.SeedLoadTestData;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.LoadTesting.SeedLoadTestData;

public sealed class LoadTestCurriculumTests
{
    private readonly LoadTestCurriculumSet _set = LoadTestCurriculum.Build("loadtest", 1, User.CreateTeacher("Load test teacher", "loadtest-teacher@loadtest.example.com"));

    [Fact]
    public void Build_ThreeUnitsOfFivePublishedLessons()
    {
        _set.Units.Select(x => x.Order).Should().Equal(1, 2, 3);
        _set.Lessons.Should().HaveCount(15).And.OnlyContain(x => x.State == LessonState.Published);
        _set.Units.Should().AllSatisfy(unit => _set.Lessons.Where(x => x.UnitId == unit.Id).Select(x => x.Order).Should().Equal(1, 2, 3, 4, 5));
    }

    [Fact]
    public void Build_EveryQuestionApprovedAndServable()
    {
        var lessonsById = _set.Lessons.ToDictionary(x => x.Id);

        _set.Questions.Should().HaveCount(450);
        _set.Questions.Should().AllSatisfy(question =>
        {
            var lesson = lessonsById[question.LessonId];
            ServableQuestionSpecification.IsSatisfiedBy(question, lesson).Should().BeTrue();
            lesson.Objectives.Select(x => x.Id).Should().Contain(question.ObjectiveId.GetValueOrDefault());
        });
    }

    [Fact]
    public void Build_EvenLessonsCarryMath_OddLessonsDoNot()
    {
        var withMath = _set.Lessons.Where(x => x.Explanation.Contains("data-type=\"inline-math\"") && x.Explanation.Contains("data-type=\"block-math\""));
        var withAnyMath = _set.Lessons.Where(x => x.Explanation.Contains("-math\""));

        withMath.Select(x => x.Order).Distinct().Should().BeEquivalentTo([2, 4]);
        withAnyMath.Select(x => x.Order).Distinct().Should().BeEquivalentTo([2, 4]);
    }

    [Fact]
    public void Build_EachUnitHasTwentyMcqBlueprint()
    {
        _set.Blueprints.Select(x => x.UnitId).Should().BeEquivalentTo(_set.Units.Select(x => (Guid?)x.Id));
        _set.Blueprints.Should().AllSatisfy(blueprint =>
        {
            blueprint.GetTypeCounts().Should().Equal(new ExamTypeCount(QuestionType.Mcq, 20));
            blueprint.TimeLimitMinutes.Should().Be(30);
            blueprint.PassMark.Should().Be(50);
        });
    }
}
