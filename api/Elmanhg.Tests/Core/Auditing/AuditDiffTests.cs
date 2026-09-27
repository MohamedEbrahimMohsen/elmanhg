using Core.Auditing;
using FluentAssertions;
using System.Text.Json.Nodes;

namespace Elmanhg.Tests.Core.Auditing;

public sealed class AuditDiffTests
{
    [Fact]
    public void Serialize_NoChanges_ReturnsNull()
    {
        var diff = AuditDiff.Serialize([]);

        diff.Should().BeNull();
    }

    [Fact]
    public void Serialize_Change_WritesCamelCaseShapeWithStringKind()
    {
        var teacherId = Guid.NewGuid();
        var change = new AuditEntityChange("TeacherSubject", Guid.NewGuid(), AuditChangeKind.Created, new Dictionary<string, AuditValueChange> { ["TeacherId"] = new(null, JsonValue.Create(teacherId)) });

        var diff = JsonNode.Parse(AuditDiff.Serialize([change])!)!;

        diff[0]!["entityType"]!.GetValue<string>().Should().Be("TeacherSubject");
        diff[0]!["change"]!.GetValue<string>().Should().Be("Created");
        diff[0]!["properties"]!["teacherId"]!["before"].Should().BeNull();
        diff[0]!["properties"]!["teacherId"]!["after"]!.GetValue<string>().Should().Be(teacherId.ToString());
    }
}
