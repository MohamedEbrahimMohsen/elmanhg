using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subjects;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Subjects;

public sealed class SubjectTests
{
    private readonly Guid _createdBy = Guid.NewGuid();

    [Fact]
    public void Create_Always_SetsNameAndCreator()
    {
        var createdBy = Guid.NewGuid();

        var subject = Subject.Create("Physics", 1, createdBy);

        subject.Name.Should().Be("Physics");
        subject.Order.Should().Be(1);
        subject.CreatedBy.Should().Be(createdBy);
        subject.Id.Should().NotBeEmpty();
        subject.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_NameWithSurroundingSpaces_StoresTrimmedName()
    {
        var subject = Subject.Create("  Physics ", 1, _createdBy);

        subject.Name.Should().Be("Physics");
    }

    [Fact]
    public void Create_OrderZero_ThrowsContentOrderInvalid()
    {
        var act = () => Subject.Create("Physics", 0, _createdBy);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ContentOrderInvalid);
    }

    [Fact]
    public void Rename_NewName_SetsTrimmedNameAndUpdater()
    {
        var subject = Subject.Create("Physics", 1, _createdBy);
        var updatedBy = Guid.NewGuid();

        subject.Rename(" Chemistry ", updatedBy);

        subject.Name.Should().Be("Chemistry");
        subject.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void MoveTo_NewOrder_SetsOrderAndUpdater()
    {
        var subject = Subject.Create("Physics", 1, _createdBy);
        var updatedBy = Guid.NewGuid();

        subject.MoveTo(3, updatedBy);

        subject.Order.Should().Be(3);
        subject.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void MoveTo_SameOrder_LeavesUpdaterUnchanged()
    {
        var subject = Subject.Create("Physics", 2, _createdBy);

        subject.MoveTo(2, Guid.NewGuid());

        subject.Order.Should().Be(2);
        subject.UpdatedBy.Should().Be(_createdBy);
    }

    [Fact]
    public void MoveTo_OrderZero_ThrowsContentOrderInvalid()
    {
        var subject = Subject.Create("Physics", 2, _createdBy);

        var act = () => subject.MoveTo(0, Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ContentOrderInvalid);
        subject.Order.Should().Be(2);
    }

    [Fact]
    public void Delete_NoUnits_SoftDeletesAndSetsUpdater()
    {
        var subject = Subject.Create("Physics", 1, _createdBy);
        var deletedBy = Guid.NewGuid();

        subject.Delete(false, deletedBy);

        subject.IsDeleted.Should().BeTrue();
        subject.UpdatedBy.Should().Be(deletedBy);
    }

    [Fact]
    public void Delete_HasUnits_ThrowsSubjectHasUnits()
    {
        var subject = Subject.Create("Physics", 1, _createdBy);

        var act = () => subject.Delete(true, Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SubjectHasUnits);
        subject.IsDeleted.Should().BeFalse();
    }
}
