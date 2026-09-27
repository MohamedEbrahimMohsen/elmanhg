using Elmanhg.Infrastructure.Data.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Elmanhg.Tests.Integration.Infrastructure.ApiFactory))]

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string PostgresImage = "pgvector/pgvector:pg17";
    private const string TestingEnvironment = "Testing";
    // Signs nothing outside this in-memory host; the JwtBearer options delegate only requires it to be non-empty.
    private const string TestJwtKey = "elmanhg-tests-signing-key-not-a-secret-0123456789";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder(PostgresImage).Build();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingEnvironment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DbConnectionString"] = _database.GetConnectionString(),
            ["CoreJwt:Issuer"] = "Elmanhg.Tests",
            ["CoreJwt:Audience"] = "Elmanhg.Tests",
            ["CoreJwt:Key"] = TestJwtKey,
            ["CoreAuditing:Enabled"] = "false",
        }));
    }

    public new async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync();
        await base.DisposeAsync();
    }
}
