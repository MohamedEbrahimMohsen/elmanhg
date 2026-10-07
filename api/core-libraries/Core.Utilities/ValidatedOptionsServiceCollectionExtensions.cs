using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Core.Utilities;

public static class ValidatedOptionsServiceCollectionExtensions
{
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(this IServiceCollection services, string sectionName) where TOptions : class
        => services.AddOptions<TOptions>()
                   .BindConfiguration(sectionName)
                   .ValidateDataAnnotations()
                   .ValidateOnStart();

    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions, TValidator>(this IServiceCollection services, string sectionName) where TOptions : class where TValidator : class, IValidateOptions<TOptions>
    {
        var builder = services.AddValidatedOptions<TOptions>(sectionName);
        services.AddSingleton<IValidateOptions<TOptions>, TValidator>();
        return builder;
    }
}
