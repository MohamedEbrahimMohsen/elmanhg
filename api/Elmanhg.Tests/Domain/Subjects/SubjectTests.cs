using Elmanhg.Domain.Subjects;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Subjects;

public sealed class SubjectTests
{
    [Fact]
    public void Create_Always_SetsNameAndCreator()
    {
        var createdBy = Guid.NewGuid();

        var subject = Subject.Create("Physics", createdBy);

        subject.Name.Should().Be("Physics");
        subject.CreatedBy.Should().Be(createdBy);
        subject.Id.Should().NotBeEmpty();
        subject.IsDeleted.Should().BeFalse();
    }
}
