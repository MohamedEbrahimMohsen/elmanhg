using Elmanhg.Application.Shared.AiService;
using Elmanhg.Infrastructure.AiService;
using FluentAssertions;
using System.Text.Json;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class AiJsonSerializerOptionsTests
{
    [Fact]
    public void Default_EnumValue_SerializesCamelCase()
    {
        var json = JsonSerializer.Serialize(new RoleProbe(AiChatRole.Assistant, null), AiJsonSerializerOptions.Default);

        json.Should().Contain("\"role\":\"assistant\"");
    }

    [Fact]
    public void Default_NullProperty_IsOmitted()
    {
        var json = JsonSerializer.Serialize(new RoleProbe(AiChatRole.Assistant, null), AiJsonSerializerOptions.Default);

        json.Should().NotContain("note");
    }

    private sealed record RoleProbe(AiChatRole Role, string? Note);
}
