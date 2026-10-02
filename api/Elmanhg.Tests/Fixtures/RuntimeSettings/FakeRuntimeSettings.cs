using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Fixtures.RuntimeSettings;

public sealed class FakeRuntimeSettings : IRuntimeSettings
{
    private RuntimeSettingValues _values;

    public FakeRuntimeSettings(SubscriptionsOptions? subscriptions = null, AskTeacherOptions? askTeacher = null, EssayGradingOptions? essayGrading = null, MathStepGradingOptions? mathStepGrading = null, ExamsOptions? exams = null, SlaCalendarOptions? slaCalendar = null)
    {
        var subscriptionsOptions = Options.Create(subscriptions ?? new SubscriptionsOptions());
        var askTeacherOptions = Options.Create(askTeacher ?? new AskTeacherOptions());
        Registry = new RuntimeSettingRegistry(
        [
            new FeatureFlagRuntimeSettings(Options.Create(exams ?? new ExamsOptions())),
            new AskTeacherRuntimeSettings(askTeacherOptions, subscriptionsOptions),
            new OutOfAppReminderRuntimeSettings(askTeacherOptions),
            // Unit tests assert wall-clock deadlines; calendar behaviour is tested with explicit options.
            new SlaCalendarRuntimeSettings(Options.Create(slaCalendar ?? new SlaCalendarOptions { SkipWeekends = false })),
            new PlanLimitRuntimeSettings(subscriptionsOptions),
            new GradingRuntimeSettings(Options.Create(essayGrading ?? new EssayGradingOptions()), Options.Create(mathStepGrading ?? new MathStepGradingOptions())),
            new UploadRuntimeSettings(askTeacherOptions),
        ]);
        _values = RuntimeSettingValues.Defaults(Registry);
    }

    public RuntimeSettingRegistry Registry { get; }

    public static RuntimeSettingRegistry DefaultRegistry() => new FakeRuntimeSettings().Registry;

    public FakeRuntimeSettings Set<T>(RuntimeSettingKey<T> key, T value)
    {
        _values = _values.With(key.Name, RuntimeSettingJson.ToElement(value));
        return this;
    }

    public Task<T> GetAsync<T>(RuntimeSettingKey<T> key, CancellationToken cancellationToken) => Task.FromResult(_values.Get(key));

    public Task<RuntimeSettingValues> GetValuesAsync(CancellationToken cancellationToken) => Task.FromResult(_values);
}
