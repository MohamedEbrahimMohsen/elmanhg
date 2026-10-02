using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Shared.RuntimeSettings;

public sealed class RuntimeSettingRegistryTests
{
    private readonly RuntimeSettingRegistry _registry = FakeRuntimeSettings.DefaultRegistry();

    [Fact]
    public void Definitions_DefaultOptions_TwentyOneOrderedByGroup()
    {
        var definitions = _registry.Definitions;

        definitions.Should().HaveCount(21);
        definitions.Select(x => x.Group).Should().BeInAscendingOrder();
        definitions[0].Key.Should().Be("features.examsRequireAllLessonsOpened");
    }

    [Fact]
    public void Definitions_Every_HasArabicAndEnglishLabelAndDescription()
    {
        _registry.Definitions.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.Label.Arabic) && !string.IsNullOrWhiteSpace(x.Label.English) && !string.IsNullOrWhiteSpace(x.Description.Arabic) && !string.IsNullOrWhiteSpace(x.Description.English));
    }

    [Fact]
    public void Find_UnknownKey_ReturnsNull() => _registry.Find("nope.key").Should().BeNull();

    [Fact]
    public void Find_KnownKey_ReturnsDefinition()
    {
        var definition = _registry.Find("askTeacher.replySlaHours");

        (definition!.Type, definition.Minimum, definition.Maximum).Should().Be((RuntimeSettingType.Integer, 1m, 168m));
    }

    [Fact]
    public void Constructor_DuplicateKey_Throws()
    {
        var planLimits = new PlanLimitRuntimeSettings(Microsoft.Extensions.Options.Options.Create(new SubscriptionsOptions()));

        var act = () => new RuntimeSettingRegistry([planLimits, planLimits]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*plans.freeDailyQuizQuestions*");
    }

    [Fact]
    public void FindProblems_DefaultOutsideRange_ReportsKey()
    {
        var problems = RuntimeSettingRegistry.FindProblems([new UploadRuntimeSettings(Microsoft.Extensions.Options.Options.Create(new AskTeacherOptions { ImageMaxSizeInMb = 12 }))]);

        problems.Should().ContainSingle().Which.Should().Contain("uploads.askTeacherImageMaxSizeInMb");
    }

    [Fact]
    public void EnsureConstraintsHold_Defaults_DoesNotThrow()
    {
        var act = () => _registry.EnsureConstraintsHold(RuntimeSettingValues.Defaults(_registry));

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureConstraintsHold_Broken_ThrowsWithConstraintCode()
    {
        var broken = RuntimeSettingValues.Defaults(_registry).With("askTeacher.secondReminderAfterHours", RuntimeSettingJson.ToElement(30));

        var act = () => _registry.EnsureConstraintsHold(broken);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.AskTeacherReminderOrderInvalid);
    }
}
