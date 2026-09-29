using Elmanhg.Domain.TeacherThreads;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherThreadContextTests
{
    [Fact]
    public void FromJson_NullDocument_ThrowsInvalidOperation()
    {
        var act = () => TeacherThreadContext.FromJson("null");

        act.Should().Throw<InvalidOperationException>();
    }
}
