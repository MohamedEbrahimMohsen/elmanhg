using Microsoft.Extensions.DependencyInjection;

namespace Core.Spreadsheets;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreSpreadsheets(this IServiceCollection services, Action<SpreadsheetOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<ISpreadsheetReader, ClosedXmlSpreadsheetReader>();
        services.AddSingleton<ISpreadsheetWriter, ClosedXmlSpreadsheetWriter>();
        return services;
    }
}
