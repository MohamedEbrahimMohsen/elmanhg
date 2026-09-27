using Core.Auditing.Entities;
using Elmanhg.Application.AuditLogs.GetAuditLogs;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.AuditLogs.GetAuditLogs;

public sealed class GetAuditLogsFilterTests
{
    private static readonly DateTimeOffset Boundary = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_NoFilters_MatchesEveryEntry()
    {
        List<AuditLog> entries = [new AuditLogBuilder().Build(), new AuditLogBuilder().WithActorUserName(null).Build(), new AuditLogBuilder().WithResourceType("Question").Build()];

        var matched = Apply(Query(), entries);

        matched.Should().HaveCount(3);
    }

    [Fact]
    public void Build_Actor_MatchesCaseInsensitiveSubstring()
    {
        var admin = new AuditLogBuilder().WithActorUserName("admin@elmanhg.test").Build();
        var teacher = new AuditLogBuilder().WithActorUserName("teacher@elmanhg.test").Build();

        var matched = Apply(Query() with { Actor = "ADMIN@" }, [admin, teacher]);

        matched.Should().ContainSingle().Which.Should().BeSameAs(admin);
    }

    [Fact]
    public void Build_Actor_ExcludesEntriesWithoutActor()
    {
        var system = new AuditLogBuilder().WithActorUserName(null).Build();

        var matched = Apply(Query() with { Actor = "admin" }, [system]);

        matched.Should().BeEmpty();
    }

    [Fact]
    public void Build_WhitespaceActor_IgnoresActorFilter()
    {
        List<AuditLog> entries = [new AuditLogBuilder().Build(), new AuditLogBuilder().WithActorUserName(null).Build()];

        var matched = Apply(Query() with { Actor = "  " }, entries);

        matched.Should().HaveCount(2);
    }

    [Fact]
    public void Build_ResourceType_MatchesExactly()
    {
        var teacher = new AuditLogBuilder().WithResourceType("Teacher").Build();
        var question = new AuditLogBuilder().WithResourceType("Question").Build();

        var matched = Apply(Query() with { ResourceType = "Teacher" }, [teacher, question]);

        matched.Should().ContainSingle().Which.Should().BeSameAs(teacher);
    }

    [Fact]
    public void Build_From_IncludesBoundaryAndExcludesEarlier()
    {
        var atBoundary = new AuditLogBuilder().WithTimestamp(Boundary).Build();
        var earlier = new AuditLogBuilder().WithTimestamp(Boundary.AddSeconds(-1)).Build();

        var matched = Apply(Query() with { From = Boundary }, [atBoundary, earlier]);

        matched.Should().ContainSingle().Which.Should().BeSameAs(atBoundary);
    }

    [Fact]
    public void Build_To_ExcludesBoundary()
    {
        var atBoundary = new AuditLogBuilder().WithTimestamp(Boundary).Build();
        var earlier = new AuditLogBuilder().WithTimestamp(Boundary.AddSeconds(-1)).Build();

        var matched = Apply(Query() with { To = Boundary }, [atBoundary, earlier]);

        matched.Should().ContainSingle().Which.Should().BeSameAs(earlier);
    }

    [Fact]
    public void Build_FromWithOffset_ComparesInUtc()
    {
        var entry = new AuditLogBuilder().WithTimestamp(Boundary).Build();

        var matched = Apply(Query() with { From = new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.FromHours(2)) }, [entry]);

        matched.Should().ContainSingle();
    }

    private static GetAuditLogsQuery Query() => new(null, null, null, null);

    private static List<AuditLog> Apply(GetAuditLogsQuery query, List<AuditLog> entries) => entries.Where(GetAuditLogsFilter.Build(query).Compile()).ToList();
}
