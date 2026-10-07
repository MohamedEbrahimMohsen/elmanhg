using Core.Settings;
using FluentAssertions;
using System.Text.Json;

namespace Elmanhg.Tests.Core.Settings;

public sealed class RuntimeSettingJsonTests
{
    [Fact]
    public void TryParse_ValidJson_ReturnsElement()
    {
        var parsed = RuntimeSettingJson.TryParse("[\"a\"]", out var element);

        (parsed, element.ValueKind).Should().Be((true, JsonValueKind.Array));
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsFalse() => RuntimeSettingJson.TryParse("{", out _).Should().BeFalse();

    [Fact]
    public void Read_JsonNull_Throws()
    {
        var element = JsonDocument.Parse("null").RootElement.Clone();

        var act = () => RuntimeSettingJson.Read<string>(element);

        act.Should().Throw<InvalidOperationException>();
    }
}
