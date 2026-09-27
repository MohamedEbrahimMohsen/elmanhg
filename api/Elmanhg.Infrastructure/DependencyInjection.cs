using Core.OTP.Sms;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Infrastructure.Sms;
using Elmanhg.Infrastructure.Subjects;
using Elmanhg.Infrastructure.Teachers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<SmsOptions>().BindConfiguration(SmsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddScoped<FakeSmsSender>();
        services.AddScoped<ISmsSender>(serviceProvider => serviceProvider.GetRequiredService<IOptions<SmsOptions>>().Value.Provider switch
        {
            SmsProvider.Fake => serviceProvider.GetRequiredService<FakeSmsSender>(),
            _ => throw new InvalidOperationException("Unsupported Sms:Provider."),
        });
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<ITeacherSubjectRepository, TeacherSubjectRepository>();
        return services;
    }
}
