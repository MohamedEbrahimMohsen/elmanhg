using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Units;

public sealed class CurriculumUnitTests
{
    private readonly Guid _createdBy = Guid.NewGuid();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());

    [Fact]
    public void Create_Always_SetsSubjectNameOrderAndCreator()
    {
        var unit = CurriculumUnit.Create(_subject, "  Mechanics ", 2, _createdBy);

        unit.SubjectId.Should().Be(_subject.Id);
        unit.Name.Should().Be("Mechanics");
        unit.Order.Should().Be(2);
        unit.CreatedBy.Should().Be(_createdBy);
        unit.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_OrderZero_ThrowsContentOrderInvalid()
    {
        var act = () => CurriculumUnit.Create(_subject, "Mechanics", 0, _createdBy);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ContentOrderInvalid);
    }

    [Fact]
    public void Rename_NewName_SetsTrimmedNameAndUpdater()
    {
        var unit = CurriculumUnit.Create(_subject, "Mechanics", 1, _createdBy);
        var updatedBy = Guid.NewGuid();

        unit.Rename(" Waves ", updatedBy);

        unit.Name.Should().Be("Waves");
        unit.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void MoveTo_NewOrder_SetsOrderAndUpdater()
    {
        var unit = CurriculumUnit.Create(_subject, "Mechanics", 1, _createdBy);
        var updatedBy = Guid.NewGuid();

        unit.MoveTo(3, updatedBy);

        unit.Order.Should().Be(3);
        unit.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void MoveTo_SameOrder_LeavesUpdaterUnchanged()
    {
        var unit = CurriculumUnit.Create(_subject, "Mechanics", 2, _createdBy);

        unit.MoveTo(2, Guid.NewGuid());

        unit.Order.Should().Be(2);
        unit.UpdatedBy.Should().Be(_createdBy);
    }

    [Fact]
    public void MoveTo_OrderZero_ThrowsContentOrderInvalid()
    {
        var unit = CurriculumUnit.Create(_subject, "Mechanics", 2, _createdBy);

        var act = () => unit.MoveTo(0, Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ContentOrderInvalid);
        unit.Order.Should().Be(2);
    }

    [Fact]
    public void Delete_NoLessons_SoftDeletesAndSetsUpdater()
    {
        var unit = CurriculumUnit.Create(_subject, "Mechanics", 1, _createdBy);
        var deletedBy = Guid.NewGuid();

        unit.Delete(false, deletedBy);

        unit.IsDeleted.Should().BeTrue();
        unit.UpdatedBy.Should().Be(deletedBy);
    }

    [Fact]
    public void Delete_HasLessons_ThrowsUnitHasLessons()
    {
        var unit = CurriculumUnit.Create(_subject, "Mechanics", 1, _createdBy);

        var act = () => unit.Delete(true, Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.UnitHasLessons);
        unit.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Delete_NoLessons_StampsDeletedAtWithUpdationDate()
    {
        var unit = CurriculumUnit.Create(_subject, "Mechanics", 1, _createdBy);

        unit.Delete(false, Guid.NewGuid());

        unit.DeletedAt.Should().NotBeNull().And.Be(unit.UpdationDate);
    }
}
