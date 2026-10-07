using Core.Errors;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.SharedKernel.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Lessons;

public sealed class LessonObjectiveTests
{
    private readonly Guid _lessonId = Guid.NewGuid();
    private readonly Guid _createdBy = Guid.NewGuid();

    [Fact]
    public void Create_Always_SetsLessonTextOrderAndCreator()
    {
        var objective = LessonObjective.Create(_lessonId, "  State the first law ", 2, _createdBy);

        objective.LessonId.Should().Be(_lessonId);
        objective.Text.Should().Be("State the first law");
        objective.Order.Should().Be(2);
        objective.CreatedBy.Should().Be(_createdBy);
    }

    [Fact]
    public void Create_OrderZero_ThrowsContentOrderInvalid()
    {
        var act = () => LessonObjective.Create(_lessonId, "State the first law", 0, _createdBy);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ContentOrderInvalid);
    }

    [Fact]
    public void Update_Changed_SetsTextOrderAndUpdater()
    {
        var objective = LessonObjective.Create(_lessonId, "State the first law", 1, _createdBy);
        var updatedBy = Guid.NewGuid();

        objective.Update(" Apply F = ma ", 3, updatedBy);

        objective.Text.Should().Be("Apply F = ma");
        objective.Order.Should().Be(3);
        objective.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void Update_Unchanged_LeavesUpdaterUnchanged()
    {
        var objective = LessonObjective.Create(_lessonId, "State the first law", 1, _createdBy);

        objective.Update("State the first law ", 1, Guid.NewGuid());

        objective.UpdatedBy.Should().Be(_createdBy);
    }

    [Fact]
    public void Update_OrderZero_ThrowsContentOrderInvalid()
    {
        var objective = LessonObjective.Create(_lessonId, "State the first law", 2, _createdBy);

        var act = () => objective.Update("State the first law", 0, Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ContentOrderInvalid);
        objective.Order.Should().Be(2);
    }

    [Fact]
    public void Delete_Always_SoftDeletesAndSetsUpdater()
    {
        var objective = LessonObjective.Create(_lessonId, "State the first law", 1, _createdBy);
        var deletedBy = Guid.NewGuid();

        objective.Delete(deletedBy);

        objective.IsDeleted.Should().BeTrue();
        objective.UpdatedBy.Should().Be(deletedBy);
    }

    [Fact]
    public void Delete_Always_StampsDeletedAtWithUpdationDate()
    {
        var objective = LessonObjective.Create(_lessonId, "State the first law", 1, _createdBy);

        objective.Delete(Guid.NewGuid());

        objective.DeletedAt.Should().NotBeNull().And.Be(objective.UpdationDate);
    }
}
