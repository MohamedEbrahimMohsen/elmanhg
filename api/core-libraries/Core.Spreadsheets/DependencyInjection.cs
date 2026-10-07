using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Core.Spreadsheets;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreSpreadsheets(this IServiceCollection services, Action<SpreadsheetOptions> configure)
    {
        services.AddOptions<SpreadsheetOptions>().Configure(configure).ValidateOnStart();
        services.AddSingleton<IValidateOptions<SpreadsheetOptions>, SpreadsheetOptionsValidator>();
        services.AddSingleton<ISpreadsheetReader, ClosedXmlSpreadsheetReader>();
        services.AddSingleton<ISpreadsheetWriter, ClosedXmlSpreadsheetWriter>();
        return services;
    }
}
