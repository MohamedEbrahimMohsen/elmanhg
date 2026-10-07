using Core.Settings;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Shared.RuntimeSettings;

public sealed class OutOfAppReminderRuntimeSettingsTests
{
    [Fact]
    public void Definitions_DefaultsFromAskTeacherOptions_InAskTeacherGroup()
    {
        var definitions = Definitions(new AskTeacherOptions());

        definitions.Select(x => (x.Key, x.Group, x.Type)).Should().Equal(("askTeacher.outOfAppReminderEnabled", nameof(RuntimeSettingGroup.AskTeacher), RuntimeSettingType.Boolean), ("askTeacher.outOfAppReminderChannels", nameof(RuntimeSettingGroup.AskTeacher), RuntimeSettingType.Choice), ("askTeacher.outOfAppReminderStage", nameof(RuntimeSettingGroup.AskTeacher), RuntimeSettingType.Choice));
        definitions.Select(x => x.DefaultValue.GetRawText()).Should().Equal("true", "\"Both\"", "\"SecondReminder\"");
        definitions[0].AllowedValues.Should().BeEmpty();
        definitions[1].AllowedValues.Should().Equal("WhatsApp", "Email", "Both");
        definitions[2].AllowedValues.Should().Equal("FirstReminder", "SecondReminder");
    }

    [Fact]
    public void Definitions_ConfiguredDefaults_AreUsed()
    {
        var definitions = Definitions(new AskTeacherOptions { OutOfAppReminderEnabled = false, OutOfAppReminderChannels = "Email", OutOfAppReminderStage = "FirstReminder" });

        definitions.Select(x => x.DefaultValue.GetRawText()).Should().Equal("false", "\"Email\"", "\"FirstReminder\"");
    }

    private static IReadOnlyList<RuntimeSettingDefinition> Definitions(AskTeacherOptions options) => new OutOfAppReminderRuntimeSettings(Microsoft.Extensions.Options.Options.Create(options)).Definitions;
}
