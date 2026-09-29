using Elmanhg.Application.TeacherInbox.Shared;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.Shared;

public sealed class TeacherInboxQueryShapeTests
{
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly Guid _physicsId = Guid.NewGuid();
    private readonly Guid _mathId = Guid.NewGuid();

    [Fact]
    public void Filter_TeacherScope_KeepsOnlyAssignedSubjects()
    {
        var physics = ThreadIn(_physicsId).Build();
        var math = ThreadIn(_mathId).Build();

        var kept = Apply([_physicsId], TeacherInboxFilter.All, physics, math);

        kept.Should().Equal(physics);
    }

    [Fact]
    public void Filter_EmptyAssignment_KeepsNothing()
    {
        var kept = Apply([], TeacherInboxFilter.All, ThreadIn(_physicsId).Build(), ThreadIn(_mathId).Build());

        kept.Should().BeEmpty();
    }

    [Fact]
    public void Filter_AdminScope_KeepsEverySubject()
    {
        var physics = ThreadIn(_physicsId).Build();
        var math = ThreadIn(_mathId).Build();

        var kept = Apply(null, TeacherInboxFilter.All, physics, math);

        kept.Should().Equal(physics, math);
    }

    [Fact]
    public void Filter_Unclaimed_KeepsOnlyThreadsWithoutTeacher()
    {
        var unclaimed = ThreadIn(_physicsId).Build();
        var claimed = ThreadIn(_physicsId).ClaimedBy(_callerId).Build();

        var kept = Apply([_physicsId], TeacherInboxFilter.Unclaimed, unclaimed, claimed);

        kept.Should().Equal(unclaimed);
    }

    [Fact]
    public void Filter_Mine_KeepsOnlyThreadsClaimedByCaller()
    {
        var unclaimed = ThreadIn(_physicsId).Build();
        var mine = ThreadIn(_physicsId).ClaimedBy(_callerId).Build();
        var others = ThreadIn(_physicsId).ClaimedBy(Guid.NewGuid()).Build();

        var kept = Apply([_physicsId], TeacherInboxFilter.Mine, unclaimed, mine, others);

        kept.Should().Equal(mine);
    }

    [Fact]
    public void Order_OpenThreadsFirstByDueTimeThenOthers()
    {
        var answered = ThreadIn(_physicsId).SubmittedAt(TeacherThreadBuilder.DefaultSubmittedAt.AddHours(-10)).AnsweredBy(_callerId).Build();
        var openLater = ThreadIn(_physicsId).SubmittedAt(TeacherThreadBuilder.DefaultSubmittedAt.AddHours(2)).Build();
        var openEarlier = ThreadIn(_physicsId).SubmittedAt(TeacherThreadBuilder.DefaultSubmittedAt).Build();

        var ordered = TeacherInboxQueryShape.Order(new[] { answered, openLater, openEarlier }.AsQueryable()).ToList();

        ordered.Should().Equal(openEarlier, openLater, answered);
    }

    private TeacherThreadBuilder ThreadIn(Guid subjectId) => new TeacherThreadBuilder().WithContext(new TeacherThreadContext(subjectId, "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", null, null, null, null));

    private List<TeacherThread> Apply(List<Guid>? subjectIds, TeacherInboxFilter filter, params TeacherThread[] threads)
    {
        var predicate = TeacherInboxQueryShape.Filter(subjectIds, filter, _callerId).Compile();
        return threads
            .Where(predicate)
            .ToList();
    }
}
