using Core.Errors;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Lessons;

public sealed class LessonLifecycleTests
{
    private readonly Guid _createdBy = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly CurriculumUnit _unit = CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid());

    [Fact]
    public void Create_Always_HasNoPublishedAtAndNoEvents()
    {
        var lesson = NewLesson();

        lesson.PublishedAt.Should().BeNull();
        lesson.GetDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void Publish_Draft_PublishesStampsAndRaisesLessonPublished()
    {
        var lesson = NewLesson();

        lesson.Publish(_actor);

        lesson.State.Should().Be(LessonState.Published);
        lesson.PublishedAt.Should().NotBeNull();
        lesson.UpdatedBy.Should().Be(_actor);
        lesson.GetDomainEvents().Should().Equal(new LessonPublished(lesson.Id, lesson.UnitId));
    }

    [Fact]
    public void Publish_Archived_RepublishesAndRaisesLessonPublished()
    {
        var lesson = NewLesson();
        lesson.Publish(_createdBy);
        lesson.Archive(_createdBy);
        lesson.ClearDomainEvents();

        lesson.Publish(_actor);

        lesson.State.Should().Be(LessonState.Published);
        lesson.GetDomainEvents().Should().Equal(new LessonPublished(lesson.Id, lesson.UnitId));
    }

    [Fact]
    public void Publish_Published_ThrowsLessonAlreadyPublished()
    {
        var lesson = NewLesson();
        lesson.Publish(_createdBy);
        lesson.ClearDomainEvents();

        var act = () => lesson.Publish(_actor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.LessonAlreadyPublished);
        lesson.GetDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void Unpublish_Published_ReturnsToDraftKeepsPublishedAtAndRaisesLessonUnpublished()
    {
        var lesson = NewLesson();
        lesson.Publish(_createdBy);
        var publishedAt = lesson.PublishedAt;
        lesson.ClearDomainEvents();

        lesson.Unpublish(_actor);

        lesson.State.Should().Be(LessonState.Draft);
        lesson.PublishedAt.Should().Be(publishedAt);
        lesson.GetDomainEvents().Should().Equal(new LessonUnpublished(lesson.Id, lesson.UnitId));
    }

    [Fact]
    public void Unpublish_Archived_ReturnsToDraftWithoutEvent()
    {
        var lesson = NewLesson();
        lesson.Publish(_createdBy);
        lesson.Archive(_createdBy);
        lesson.ClearDomainEvents();

        lesson.Unpublish(_actor);

        lesson.State.Should().Be(LessonState.Draft);
        lesson.GetDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void Unpublish_Draft_ThrowsLessonAlreadyDraft()
    {
        var lesson = NewLesson();

        var act = () => lesson.Unpublish(_actor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.LessonAlreadyDraft);
    }

    [Fact]
    public void Archive_Published_ArchivesAndRaisesLessonArchived()
    {
        var lesson = NewLesson();
        lesson.Publish(_createdBy);
        lesson.ClearDomainEvents();

        lesson.Archive(_actor);

        lesson.State.Should().Be(LessonState.Archived);
        lesson.UpdatedBy.Should().Be(_actor);
        lesson.GetDomainEvents().Should().Equal(new LessonArchived(lesson.Id, lesson.UnitId));
    }

    [Fact]
    public void Archive_Draft_ThrowsLessonNotPublished()
    {
        var lesson = NewLesson();

        var act = () => lesson.Archive(_actor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.LessonNotPublished);
        lesson.State.Should().Be(LessonState.Draft);
    }

    [Fact]
    public void Archive_Archived_ThrowsLessonAlreadyArchived()
    {
        var lesson = NewLesson();
        lesson.Publish(_createdBy);
        lesson.Archive(_createdBy);

        var act = () => lesson.Archive(_actor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.LessonAlreadyArchived);
    }

    [Fact]
    public void MoveTo_NewOrder_SetsOrderAndUpdater()
    {
        var lesson = NewLesson();

        lesson.MoveTo(3, _actor);

        lesson.Order.Should().Be(3);
        lesson.UpdatedBy.Should().Be(_actor);
    }

    [Fact]
    public void MoveTo_SameOrder_LeavesUpdaterUnchanged()
    {
        var lesson = NewLesson();

        lesson.MoveTo(1, _actor);

        lesson.UpdatedBy.Should().Be(_createdBy);
    }

    [Fact]
    public void MoveTo_OrderZero_ThrowsContentOrderInvalid()
    {
        var lesson = NewLesson();

        var act = () => lesson.MoveTo(0, _actor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ContentOrderInvalid);
    }

    [Fact]
    public void Delete_DraftWithObjectives_SoftDeletesLessonAndObjectives()
    {
        var lesson = NewLesson();
        lesson.Update("Newton's laws", string.Empty, string.Empty, null, [new LessonObjectiveContent(null, "First"), new LessonObjectiveContent(null, "Second")], _createdBy);

        lesson.Delete(false, _actor);

        lesson.IsDeleted.Should().BeTrue();
        lesson.UpdatedBy.Should().Be(_actor);
        lesson.Objectives.Should().HaveCount(2).And.OnlyContain(x => x.IsDeleted);
    }

    [Fact]
    public void Delete_Archived_SoftDeletes()
    {
        var lesson = NewLesson();
        lesson.Publish(_createdBy);
        lesson.Archive(_createdBy);

        lesson.Delete(false, _actor);

        lesson.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Delete_Published_ThrowsLessonIsPublished()
    {
        var lesson = NewLesson();
        lesson.Publish(_createdBy);

        var act = () => lesson.Delete(false, _actor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.LessonIsPublished);
        lesson.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Delete_HasQuestions_ThrowsLessonHasQuestions()
    {
        var lesson = NewLesson();

        var act = () => lesson.Delete(true, _actor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.LessonHasQuestions);
        lesson.IsDeleted.Should().BeFalse();
    }

    private Lesson NewLesson() => Lesson.Create(_unit, "Newton's laws", 1, _createdBy);

    [Fact]
    public void Delete_Draft_StampsDeletedAtWithUpdationDate()
    {
        var lesson = NewLesson();
        lesson.Update("Newton's laws", string.Empty, string.Empty, null, [new LessonObjectiveContent(null, "First")], _createdBy);

        lesson.Delete(false, _actor);

        lesson.DeletedAt.Should().NotBeNull().And.Be(lesson.UpdationDate);
        lesson.Objectives.Should().OnlyContain(x => x.DeletedAt != null);
    }
}
