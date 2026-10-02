using Elmanhg.Domain.RuntimeSettings;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.RuntimeSettings;

public sealed class RuntimeSettingOverrideTests
{
    private static readonly Guid AdminId = Guid.Parse("6a1c2e3f-4b5d-4e6f-8a9b-0c1d2e3f4a5b");
    private static readonly Guid OtherAdminId = Guid.Parse("7b2d3f4a-5c6e-4f7a-9b0c-1d2e3f4a5b6c");

    [Fact]
    public void Create_SetsKeyValueAndCreator()
    {
        var row = RuntimeSettingOverride.Create("plans.freeDailyQuizQuestions", "12", AdminId);

        (row.Key, row.Value, row.CreatedBy, row.IsOverridden).Should().Be(("plans.freeDailyQuizQuestions", "12", AdminId, true));
    }

    [Fact]
    public void Override_ReplacesValueAndStampsUpdate()
    {
        var row = RuntimeSettingOverride.Create("plans.freeDailyQuizQuestions", "12", AdminId);
        var before = row.UpdationDate;

        row.Override("15", OtherAdminId);

        (row.Value, row.UpdatedBy).Should().Be(("15", OtherAdminId));
        row.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Reset_ClearsValueAndStampsUpdate()
    {
        var row = RuntimeSettingOverride.Create("plans.freeDailyQuizQuestions", "12", AdminId);
        var before = row.UpdationDate;

        row.Reset(OtherAdminId);

        row.Value.Should().BeNull();
        row.IsOverridden.Should().BeFalse();
        row.UpdatedBy.Should().Be(OtherAdminId);
        row.UpdationDate.Should().BeOnOrAfter(before);
    }
}
