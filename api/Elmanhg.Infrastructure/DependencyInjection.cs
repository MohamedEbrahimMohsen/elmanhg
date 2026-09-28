using Core.OTP.Sms;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Application.Shared.Spreadsheets;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using Elmanhg.Infrastructure.Identity;
using Elmanhg.Infrastructure.Lessons;
using Elmanhg.Infrastructure.Questions;
using Elmanhg.Infrastructure.RichText;
using Elmanhg.Infrastructure.Sms;
using Elmanhg.Infrastructure.Spreadsheets;
using Elmanhg.Infrastructure.Storage;
using Elmanhg.Infrastructure.Subjects;
using Elmanhg.Infrastructure.Teachers;
using Elmanhg.Infrastructure.Units;
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
        services.AddOptions<FileStorageOptions>().BindConfiguration(FileStorageOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddScoped<LocalDiskFileStorage>();
        services.AddScoped<IFileStorage>(serviceProvider => serviceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value.Provider switch
        {
            FileStorageProvider.Local => serviceProvider.GetRequiredService<LocalDiskFileStorage>(),
            _ => throw new InvalidOperationException("Unsupported FileStorage:Provider."),
        });
        services.AddSingleton<IRichTextSanitizer, RichTextSanitizer>();
        services.AddSingleton<ISpreadsheetReader, ClosedXmlSpreadsheetReader>();
        services.AddSingleton<ISpreadsheetWriter, ClosedXmlSpreadsheetWriter>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<ITeacherSubjectRepository, TeacherSubjectRepository>();
        services.AddScoped<ICurriculumUnitRepository, CurriculumUnitRepository>();
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IQuestionImportBatchRepository, QuestionImportBatchRepository>();
        return services;
    }
}
