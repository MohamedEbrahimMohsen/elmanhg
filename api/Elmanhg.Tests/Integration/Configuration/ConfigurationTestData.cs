using Core.Auditing.Entities;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Domain.RuntimeSettings;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Configuration;

public static class ConfigurationTestData
{
    public const string SettingsRoute = "/api/configuration/settings";
    public const string InfrastructureRoute = "/api/configuration/infrastructure";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static string SettingRoute(string key) => $"{SettingsRoute}/{key}";

    public static async Task<HttpClient> AdminClientAsync(ApiFactory factory)
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken).ConfigureAwait(false);
    }

    public static async Task<HttpClient> TeacherClientAsync(ApiFactory factory)
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken).ConfigureAwait(false);
    }

    public static async Task ClearOverridesAsync(ApiFactory factory)
    {
        var keys = factory.Services.GetRequiredService<RuntimeSettingRegistry>().Definitions
            .Select(x => x.Key)
            .ToList();
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().RuntimeSettingOverrides.Where(x => keys.Contains(x.Key)).ExecuteDeleteAsync(CancellationToken).ConfigureAwait(false);
        }

        factory.Services.GetRequiredService<IMemoryCache>().Remove(RuntimeSettingsCache.Key);
    }

    public static async Task<RuntimeSettingOverride?> ReadOverrideAsync(ApiFactory factory, string key)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().RuntimeSettingOverrides.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, CancellationToken).ConfigureAwait(false);
    }

    public static async Task<List<AuditLog>> ReadAuditsAsync(ApiFactory factory, string action, Guid resourceId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking().Where(x => x.Action == action && x.ResourceId == resourceId).OrderBy(x => x.Timestamp).ToListAsync(CancellationToken).ConfigureAwait(false);
    }
}
