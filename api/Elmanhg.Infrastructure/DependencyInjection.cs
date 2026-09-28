using Elmanhg.Application.Shared.RichText;
using Elmanhg.Application.Shared.Spreadsheets;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.ReviewSessions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using Elmanhg.Infrastructure.ExamBlueprints;
using Elmanhg.Infrastructure.Identity;
using Elmanhg.Infrastructure.Lessons;
using Elmanhg.Infrastructure.Mastery;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Infrastructure.Questions;
using Elmanhg.Infrastructure.ReviewSessions;
using Elmanhg.Infrastructure.RichText;
using Elmanhg.Infrastructure.Sessions;
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
        services.AddOtpDelivery();
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
        services.AddScoped<IReviewSessionRepository, ReviewSessionRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IQuestionMasteryRepository, QuestionMasteryRepository>();
        services.AddScoped<IExamBlueprintRepository, ExamBlueprintRepository>();
        return services;
    }
}
