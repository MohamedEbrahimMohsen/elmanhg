using Elmanhg.Application.Shared.TrainingData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.TrainingData;

public static class TrainingDataServiceCollectionExtensions
{
    public static IServiceCollection AddTrainingData(this IServiceCollection services)
    {
        services.AddOptions<TrainingDataOptions>().BindConfiguration(TrainingDataOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<TrainingDataOptions>, TrainingDataOptionsValidator>();
        services.AddSingleton<IStudentIdHasher, HmacStudentIdHasher>();
        return services;
    }
}
