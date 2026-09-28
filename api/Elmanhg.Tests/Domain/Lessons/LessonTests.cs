using Core.Errors;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Lessons;

public sealed class LessonTests
{
    private readonly Guid _createdBy = Guid.NewGuid();
    private readonly CurriculumUnit _unit = CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid());

    [Fact]
    public void Create_Always_SetsUnitNameOrderAndDraftState()
    {
        var lesson = Lesson.Create(_unit, "  Newton's laws ", 2, _createdBy);

        lesson.UnitId.Should().Be(_unit.Id);
        lesson.Name.Should().Be("Newton's laws");
        lesson.Order.Should().Be(2);
        lesson.State.Should().Be(LessonState.Draft);
        lesson.Explanation.Should().BeEmpty();
        lesson.Summary.Should().BeEmpty();
        lesson.VideoUrl.Should().BeNull();
        lesson.Objectives.Should().BeEmpty();
        lesson.CreatedBy.Should().Be(_createdBy);
    }

    [Fact]
    public void Create_OrderZero_ThrowsContentOrderInvalid()
    {
        var act = () => Lesson.Create(_unit, "Newton's laws", 0, _createdBy);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ContentOrderInvalid);
    }

    [Fact]
    public void Update_NewContent_SetsFieldsAndUpdater()
    {
        var lesson = Lesson.Create(_unit, "Newton's laws", 1, _createdBy);
        var updatedBy = Guid.NewGuid();

        lesson.Update(" Momentum ", "<p>Explanation</p>", "<p>Summary</p>", " https://example.com/video ", [], updatedBy);

        lesson.Name.Should().Be("Momentum");
        lesson.Explanation.Should().Be("<p>Explanation</p>");
        lesson.Summary.Should().Be("<p>Summary</p>");
        lesson.VideoUrl.Should().Be("https://example.com/video");
        lesson.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void Update_BlankVideoUrl_StoresNull()
    {
        var lesson = Lesson.Create(_unit, "Newton's laws", 1, _createdBy);
        lesson.Update("Newton's laws", string.Empty, string.Empty, "https://example.com/video", [], _createdBy);

        lesson.Update("Newton's laws", string.Empty, string.Empty, "  ", [], _createdBy);

        lesson.VideoUrl.Should().BeNull();
    }

    [Fact]
    public void Update_NewObjectives_AddsThemInListOrder()
    {
        var lesson = Lesson.Create(_unit, "Newton's laws", 1, _createdBy);
        var updatedBy = Guid.NewGuid();

        lesson.Update("Newton's laws", string.Empty, string.Empty, null, [new LessonObjectiveContent(null, " State the first law "), new LessonObjectiveContent(null, "Apply F = ma")], updatedBy);

        lesson.Objectives.Should().HaveCount(2);
        lesson.Objectives.Select(x => x.Order).Should().Equal(1, 2);
        lesson.Objectives.Select(x => x.Text).Should().Equal("State the first law", "Apply F = ma");
        lesson.Objectives.Should().OnlyContain(x => x.LessonId == lesson.Id && x.CreatedBy == updatedBy);
    }

    [Fact]
    public void Update_ExistingObjectiveMoved_KeepsIdAndUpdatesTextAndOrder()
    {
        var lesson = Lesson.Create(_unit, "Newton's laws", 1, _createdBy);
        lesson.Update("Newton's laws", string.Empty, string.Empty, null, [new LessonObjectiveContent(null, "First"), new LessonObjectiveContent(null, "Second")], _createdBy);
        var second = lesson.Objectives[1];

        lesson.Update("Newton's laws", string.Empty, string.Empty, null, [new LessonObjectiveContent(second.Id, "Second, edited"), new LessonObjectiveContent(lesson.Objectives[0].Id, "First")], _createdBy);

        second.Id.Should().Be(lesson.Objectives[1].Id);
        second.Text.Should().Be("Second, edited");
        second.Order.Should().Be(1);
    }

    [Fact]
    public void Update_ObjectiveLeftOut_SoftDeletesIt()
    {
        var lesson = Lesson.Create(_unit, "Newton's laws", 1, _createdBy);
        lesson.Update("Newton's laws", string.Empty, string.Empty, null, [new LessonObjectiveContent(null, "First"), new LessonObjectiveContent(null, "Second")], _createdBy);
        var first = lesson.Objectives[0];
        var updatedBy = Guid.NewGuid();

        lesson.Update("Newton's laws", string.Empty, string.Empty, null, [new LessonObjectiveContent(lesson.Objectives[1].Id, "Second")], updatedBy);

        first.IsDeleted.Should().BeTrue();
        first.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void Update_UnknownObjectiveId_ThrowsLessonObjectiveUnknown()
    {
        var lesson = Lesson.Create(_unit, "Newton's laws", 1, _createdBy);
        lesson.Update("Newton's laws", string.Empty, string.Empty, null, [new LessonObjectiveContent(null, "First")], _createdBy);

        var act = () => lesson.Update("Momentum", string.Empty, string.Empty, null, [new LessonObjectiveContent(Guid.NewGuid(), "Unknown")], _createdBy);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.LessonObjectiveUnknown);
        lesson.Name.Should().Be("Newton's laws");
        lesson.Objectives.Should().OnlyContain(x => !x.IsDeleted);
    }
}
