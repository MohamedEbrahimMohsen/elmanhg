using Core.Cache;
using Core.Identity;
using Core.Identity.Tokens.RefreshToken;
using Core.Settings;
using Core.Utilities;
using Elmanhg.Application.Questions.ImportQuestions;
using Elmanhg.Application.Shared.Analytics;
using Elmanhg.Application.Shared.Authorization;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Elmanhg.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SubjectScopeBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UserActivityBehaviour<,>));
        services.AddCoreCache();
        services.AddTransient<IPipelineBehavior<ImportQuestionsCommand, ImportQuestionsResult>, ImportQuestionsReplayBehaviour>();
        services.AddValidatedOptions<AuthOptions>(AuthOptions.SectionName);
        services.AddCoreRefreshTokenRotation<User>();
        services.AddOptions<RefreshTokenRotationOptions>().Configure<IOptions<AuthOptions>>((rotation, auth) => rotation.ReuseGrace = TimeSpan.FromSeconds(auth.Value.RefreshTokenReuseGraceSeconds));
        services.AddOptions<AdminSeedOptions>().BindConfiguration(AdminSeedOptions.SectionName);
        services.AddValidatedOptions<AuditLogsOptions>(AuditLogsOptions.SectionName);
        services.AddValidatedOptions<ContentOptions>(ContentOptions.SectionName);
        services.AddValidatedOptions<QuestionValidationOptions>(QuestionValidationOptions.SectionName);
        services.AddValidatedOptions<SessionsOptions>(SessionsOptions.SectionName)
            .Validate(x => x.MinQuizSize <= x.DefaultQuizSize && x.DefaultQuizSize <= x.MaxQuizSize, "Sessions:MinQuizSize <= DefaultQuizSize <= MaxQuizSize is required.");
        services.AddValidatedOptions<MasteryOptions>(MasteryOptions.SectionName);
        services.AddValidatedOptions<ProgressOptions>(ProgressOptions.SectionName)
            .Validate(x => TimeZoneInfo.TryFindSystemTimeZoneById(x.StreakTimeZone, out _), "Progress:StreakTimeZone must be a known IANA time zone id.");
        services.AddValidatedOptions<ExamBlueprintsOptions>(ExamBlueprintsOptions.SectionName);
        services.AddValidatedOptions<ExamsOptions>(ExamsOptions.SectionName);
        services.AddValidatedOptions<SubscriptionsOptions>(SubscriptionsOptions.SectionName)
            .Validate(x => x.BasePrices.Count > 0 && x.BasePrices.Values.All(price => price.Months is >= 1 and <= 36 && price.AmountMinor > 0), "Subscriptions:BasePrices needs at least one period, each with Months 1-36 and AmountMinor > 0.")
            .Validate(x => TimeZoneInfo.TryFindSystemTimeZoneById(x.DailyQuotaTimeZone, out _), "Subscriptions:DailyQuotaTimeZone must be a known IANA time zone id.");
        services.AddValidatedOptions<StudentsOptions>(StudentsOptions.SectionName);
        services.AddValidatedOptions<UsersOptions>(UsersOptions.SectionName);
        services.AddValidatedOptions<AskTeacherOptions, AskTeacherOptionsValidator>(AskTeacherOptions.SectionName);
        services.AddValidatedOptions<AnalyticsOptions>(AnalyticsOptions.SectionName);
        services.AddValidatedOptions<DashboardOptions>(DashboardOptions.SectionName)
            .Validate(x => TimeZoneInfo.TryFindSystemTimeZoneById(x.TimeZone, out _), "Dashboard:TimeZone must be a known IANA time zone id.")
            .Validate(x => x.DefaultRangeDays <= x.MaxRangeDays, "Dashboard:DefaultRangeDays must not exceed MaxRangeDays.");
        services.AddOptions<CachingOptions>().Configure<IOptions<DashboardOptions>>((caching, dashboard) => caching.DefaultTtl = TimeSpan.FromSeconds(dashboard.Value.CacheSeconds));
        services.AddValidatedOptions<ClientErrorsOptions>(ClientErrorsOptions.SectionName);
        services.AddValidatedOptions<ContentRetrievalOptions>(ContentRetrievalOptions.SectionName)
            .Validate(x => x.DefaultTopK <= x.MaxTopK, "ContentRetrieval:DefaultTopK must not exceed MaxTopK.");
        services.AddValidatedOptions<AvatarOptions>(AvatarOptions.SectionName)
            .Validate(x => x.MaxHistoryMessages % 2 == 0, "Avatar:MaxHistoryMessages must be even.");
        services.AddValidatedOptions<MathStepGradingOptions>(MathStepGradingOptions.SectionName);
        services.AddValidatedOptions<EssayGradingOptions>(EssayGradingOptions.SectionName);
        services.AddValidatedOptions<GradeReviewOptions>(GradeReviewOptions.SectionName);
        services.AddValidatedOptions<TrainingExportsOptions>(TrainingExportsOptions.SectionName);
        services.AddSingleton<IRuntimeSettingDefinitions, FeatureFlagRuntimeSettings>();
        services.AddSingleton<IRuntimeSettingDefinitions, AskTeacherRuntimeSettings>();
        services.AddSingleton<IRuntimeSettingDefinitions, OutOfAppReminderRuntimeSettings>();
        services.AddValidatedOptions<SlaCalendarOptions, SlaCalendarOptionsValidator>(SlaCalendarOptions.SectionName);
        services.AddSingleton<IRuntimeSettingDefinitions, SlaCalendarRuntimeSettings>();
        services.AddSingleton<IRuntimeSettingDefinitions, PlanLimitRuntimeSettings>();
        services.AddSingleton<IRuntimeSettingDefinitions, GradingRuntimeSettings>();
        services.AddSingleton<IRuntimeSettingDefinitions, UploadRuntimeSettings>();
        services.AddCoreRuntimeSettings<RuntimeSettingOverrideStore>(Enum.GetNames<RuntimeSettingGroup>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Random.Shared);
        return services;
    }
}
