using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Questions.ImportQuestions;
using Elmanhg.Application.Shared.Analytics;
using Elmanhg.Application.Shared.Authorization;
using Elmanhg.Application.Shared.Options;
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
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DashboardCacheBehaviour<,>));
        services.AddTransient<IPipelineBehavior<ImportQuestionsCommand, ImportQuestionsResult>, ImportQuestionsReplayBehaviour>();
        services.AddMemoryCache();
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AdminSeedOptions>().BindConfiguration(AdminSeedOptions.SectionName);
        services.AddOptions<AuditLogsOptions>().BindConfiguration(AuditLogsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ContentOptions>().BindConfiguration(ContentOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<QuestionValidationOptions>().BindConfiguration(QuestionValidationOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SessionsOptions>().BindConfiguration(SessionsOptions.SectionName).ValidateDataAnnotations()
            .Validate(x => x.MinQuizSize <= x.DefaultQuizSize && x.DefaultQuizSize <= x.MaxQuizSize, "Sessions:MinQuizSize <= DefaultQuizSize <= MaxQuizSize is required.")
            .ValidateOnStart();
        services.AddOptions<MasteryOptions>().BindConfiguration(MasteryOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ProgressOptions>().BindConfiguration(ProgressOptions.SectionName).ValidateDataAnnotations()
            .Validate(x => TimeZoneInfo.TryFindSystemTimeZoneById(x.StreakTimeZone, out _), "Progress:StreakTimeZone must be a known IANA time zone id.")
            .ValidateOnStart();
        services.AddOptions<ExamBlueprintsOptions>().BindConfiguration(ExamBlueprintsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ExamsOptions>().BindConfiguration(ExamsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SubscriptionsOptions>().BindConfiguration(SubscriptionsOptions.SectionName).ValidateDataAnnotations()
            .Validate(x => x.BasePrices.Count > 0 && x.BasePrices.Values.All(price => price.Months is >= 1 and <= 36 && price.AmountMinor > 0), "Subscriptions:BasePrices needs at least one period, each with Months 1-36 and AmountMinor > 0.")
            .Validate(x => TimeZoneInfo.TryFindSystemTimeZoneById(x.DailyQuotaTimeZone, out _), "Subscriptions:DailyQuotaTimeZone must be a known IANA time zone id.")
            .ValidateOnStart();
        services.AddOptions<StudentsOptions>().BindConfiguration(StudentsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<UsersOptions>().BindConfiguration(UsersOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AskTeacherOptions>().BindConfiguration(AskTeacherOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<AskTeacherOptions>, AskTeacherOptionsValidator>();
        services.AddOptions<AnalyticsOptions>().BindConfiguration(AnalyticsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<DashboardOptions>().BindConfiguration(DashboardOptions.SectionName).ValidateDataAnnotations()
            .Validate(x => TimeZoneInfo.TryFindSystemTimeZoneById(x.TimeZone, out _), "Dashboard:TimeZone must be a known IANA time zone id.")
            .Validate(x => x.DefaultRangeDays <= x.MaxRangeDays, "Dashboard:DefaultRangeDays must not exceed MaxRangeDays.")
            .ValidateOnStart();
        services.AddOptions<ClientErrorsOptions>().BindConfiguration(ClientErrorsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ContentRetrievalOptions>().BindConfiguration(ContentRetrievalOptions.SectionName).ValidateDataAnnotations()
            .Validate(x => x.DefaultTopK <= x.MaxTopK, "ContentRetrieval:DefaultTopK must not exceed MaxTopK.")
            .ValidateOnStart();
        services.AddOptions<AvatarOptions>().BindConfiguration(AvatarOptions.SectionName).ValidateDataAnnotations().Validate(x => x.MaxHistoryMessages % 2 == 0, "Avatar:MaxHistoryMessages must be even.").ValidateOnStart();
        services.AddOptions<EssayGradingOptions>().BindConfiguration(EssayGradingOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<TrainingExportsOptions>().BindConfiguration(TrainingExportsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Random.Shared);
        return services;
    }
}
