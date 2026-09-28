using Elmanhg.Domain.Sessions;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class MultiUnitExamScopeTests
{
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly Guid _first = Guid.NewGuid();
    private readonly Guid _second = Guid.NewGuid();

    [Fact]
    public void ToKey_SortsUnitIdsAndIncludesSize()
    {
        var forward = new MultiUnitExamScope(_subjectId, [_first, _second], 20).ToKey();
        var reversed = new MultiUnitExamScope(_subjectId, [_second, _first], 20).ToKey();

        forward.Should().Be(reversed).And.StartWith("units:20:");
    }

    [Fact]
    public void FromJson_RoundTripsSubjectUnitsAndSize()
    {
        var scope = new MultiUnitExamScope(_subjectId, [_second, _first], 40);

        var restored = MultiUnitExamScope.FromJson(scope.ToJson());

        (restored.SubjectId, restored.Size).Should().Be((_subjectId, 40));
        restored.UnitIds.Should().Equal(_second, _first);
    }

    [Fact]
    public void FromJsonNull_ThrowsInvalidOperation()
    {
        var act = () => MultiUnitExamScope.FromJson("null");

        act.Should().Throw<InvalidOperationException>();
    }
}
