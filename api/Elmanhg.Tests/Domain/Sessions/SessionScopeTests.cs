using Elmanhg.Domain.Sessions;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class SessionScopeTests
{
    [Fact]
    public void QuizScope_FromJson_RoundTripsLessonId()
    {
        var lessonId = Guid.NewGuid();

        var scope = QuizScope.FromJson(new QuizScope(lessonId).ToJson());

        scope.LessonId.Should().Be(lessonId);
    }

    [Fact]
    public void UnitExamScope_ToKey_UsesUnitPrefixAndLowercaseGuid()
    {
        var unitId = Guid.NewGuid();

        var key = new UnitExamScope(unitId).ToKey();

        key.Should().Be("unit:" + unitId.ToString("D"));
    }

    [Fact]
    public void UnitExamScope_FromJson_RoundTripsUnitId()
    {
        var scope = new UnitExamScope(Guid.NewGuid());

        var parsed = UnitExamScope.FromJson(scope.ToJson());

        parsed.Should().Be(scope);
    }

    [Fact]
    public void UnitExamScope_FromJsonNull_ThrowsInvalidOperation()
    {
        var act = () => UnitExamScope.FromJson("null");

        act.Should().Throw<InvalidOperationException>();
    }
}
