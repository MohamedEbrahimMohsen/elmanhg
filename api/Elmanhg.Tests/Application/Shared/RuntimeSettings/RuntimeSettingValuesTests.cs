using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Domain.RuntimeSettings;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Shared.RuntimeSettings;

public sealed class RuntimeSettingValuesTests
{
    private static readonly Guid AdminId = Guid.Parse("6a1c2e3f-4b5d-4e6f-8a9b-0c1d2e3f4a5b");
    private readonly RuntimeSettingRegistry _registry = FakeRuntimeSettings.DefaultRegistry();

    [Fact]
    public void From_NoOverrides_ReturnsOptionsDefaults()
    {
        var registry = new FakeRuntimeSettings(subscriptions: new SubscriptionsOptions { AskTeacherReplySlaHours = 30 }, essayGrading: new EssayGradingOptions { ReviewConfidenceThreshold = 0.6m }).Registry;

        var values = RuntimeSettingValues.From(registry, []);

        (values.Get(AskTeacherRuntimeSettings.ReplySlaHours), values.Get(GradingRuntimeSettings.EssayReviewConfidenceThreshold)).Should().Be((30, 0.6m));
    }

    [Fact]
    public void From_ValidOverride_ReturnsOverride()
    {
        var values = RuntimeSettingValues.From(_registry, [Row("askTeacher.replySlaHours", "36")]);

        values.Get(AskTeacherRuntimeSettings.ReplySlaHours).Should().Be(36);
    }

    [Fact]
    public void From_OverrideOutsideRange_FallsBackToDefault()
    {
        var values = RuntimeSettingValues.From(_registry, [Row("askTeacher.replySlaHours", "500")]);

        values.Get(AskTeacherRuntimeSettings.ReplySlaHours).Should().Be(24);
    }

    [Fact]
    public void From_ResetRowOrUnknownKey_IsIgnored()
    {
        var reset = Row("askTeacher.replySlaHours", "36");
        reset.Reset(AdminId);

        var values = RuntimeSettingValues.From(_registry, [reset, Row("nope.key", "1")]);

        values.Get(AskTeacherRuntimeSettings.ReplySlaHours).Should().Be(24);
    }

    [Fact]
    public void With_ReplacesOnlyThatKey()
    {
        var original = RuntimeSettingValues.Defaults(_registry);

        var changed = original.With("plans.freeDailyQuizQuestions", RuntimeSettingJson.ToElement(7));

        (changed.Get(PlanLimitRuntimeSettings.FreeDailyQuizQuestions), changed.Get(PlanLimitRuntimeSettings.FreeDailyAvatarMessages), original.Get(PlanLimitRuntimeSettings.FreeDailyQuizQuestions)).Should().Be((7, 5, 10));
    }

    [Fact]
    public void Get_EveryRegisteredKey_DeserialisesToItsType()
    {
        var values = RuntimeSettingValues.Defaults(_registry);

        var act = () => new object[]
        {
            values.Get(FeatureFlagRuntimeSettings.ExamsRequireAllLessonsOpened),
            values.Get(FeatureFlagRuntimeSettings.RefundsEnabled),
            values.Get(FeatureFlagRuntimeSettings.StudentsCanDeleteAvatarChats),
            values.Get(AskTeacherRuntimeSettings.ReplySlaHours),
            values.Get(AskTeacherRuntimeSettings.FirstReminderAfterHours),
            values.Get(AskTeacherRuntimeSettings.SecondReminderAfterHours),
            values.Get(OutOfAppReminderRuntimeSettings.Enabled),
            values.Get(OutOfAppReminderRuntimeSettings.Channels),
            values.Get(OutOfAppReminderRuntimeSettings.Stage),
            values.Get(SlaCalendarRuntimeSettings.SkipWeekends),
            values.Get(SlaCalendarRuntimeSettings.WeekendDays),
            values.Get(SlaCalendarRuntimeSettings.TimeZone),
            values.Get(PlanLimitRuntimeSettings.FreeDailyQuizQuestions),
            values.Get(PlanLimitRuntimeSettings.FreeDailyAvatarMessages),
            values.Get(PlanLimitRuntimeSettings.FreeOpenLessonsPerUnit),
            values.Get(PlanLimitRuntimeSettings.BaseDailyAvatarMessages),
            values.Get(PlanLimitRuntimeSettings.AskTeacherMonthlyQuestions),
            values.Get(GradingRuntimeSettings.EssayReviewConfidenceThreshold),
            values.Get(GradingRuntimeSettings.MathStepReviewConfidenceThreshold),
            values.Get(UploadRuntimeSettings.AskTeacherImageMaxSizeInMb),
            values.Get(UploadRuntimeSettings.VoiceReplyMaxSizeInMb),
            values.Get(UploadRuntimeSettings.VoiceReplyMaxDurationSeconds),
        };

        act().Should().HaveCount(_registry.Definitions.Count);
    }

    [Fact]
    public void Raw_UnregisteredKey_Throws()
    {
        var values = RuntimeSettingValues.Defaults(_registry);

        var act = () => values.Raw("nope.key");

        act.Should().Throw<InvalidOperationException>();
    }

    private static RuntimeSettingOverride Row(string key, string value) => RuntimeSettingOverride.Create(key, value, AdminId);
}
