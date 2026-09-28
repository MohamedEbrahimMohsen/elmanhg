using Elmanhg.Application.Questions.ImportQuestions;
using Elmanhg.Application.Shared.Authorization;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Elmanhg.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SubjectScopeBehaviour<,>));
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
        return services;
    }
}
