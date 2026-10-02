using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Domain.RuntimeSettings;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.SlaCalendars;

public static class SlaCalendarTestData
{
    public const string ExamPeriodsRoute = "/api/configuration/exam-periods";

    private static readonly Guid AdminId = Guid.Parse("3f0c8a52-7d1e-4b6a-9c2f-5e8d1a0b7c43");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static string ExamPeriodRoute(Guid id) => $"{ExamPeriodsRoute}/{id}";

    public static async Task ClearExamPeriodsAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().ExamPeriods.IgnoreQueryFilters().ExecuteDeleteAsync(CancellationToken).ConfigureAwait(false);
    }

    public static async Task SetOverrideAsync(ApiFactory factory, string key, string json)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await context.RuntimeSettingOverrides.SingleOrDefaultAsync(x => x.Key == key, CancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                context.RuntimeSettingOverrides.Add(RuntimeSettingOverride.Create(key, json, AdminId));
            }
            else
            {
                row.Override(json, AdminId);
            }

            await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        }

        factory.Services.GetRequiredService<IMemoryCache>().Remove(RuntimeSettingsCache.Key);
    }
}
