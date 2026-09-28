using Core.OTP.Sms;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Elmanhg.Tests.Integration.Infrastructure.ApiFactory))]

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string PostgresImage = "pgvector/pgvector:pg17";
    private const string TestingEnvironment = "Testing";
    // Signs nothing outside this in-memory host; the JwtBearer options delegate only requires it to be non-empty.
    private const string TestJwtKey = "elmanhg-tests-signing-key-not-a-secret-0123456789";
    // Keys only the HMAC of OTP codes inside this in-memory host; codes are read back from RecordingSmsSender.
    private const string TestOtpSecret = "elmanhg-tests-otp-secret";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder(PostgresImage).Build();

    public RecordingSmsSender Sms { get; } = new();

    public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), "elmanhg-tests-media", Guid.NewGuid().ToString("N"));

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingEnvironment);
        // AddCoreAuditing reads this flag while Program registers services, before ConfigureAppConfiguration sources are applied.
        builder.UseSetting("CoreAuditing:Enabled", "true");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DbConnectionString"] = _database.GetConnectionString(),
            ["CoreJwt:Issuer"] = "Elmanhg.Tests",
            ["CoreJwt:Audience"] = "Elmanhg.Tests",
            ["CoreJwt:Key"] = TestJwtKey,
            ["AuditLogs:MaxPageSize"] = "100",
            ["AuditLogs:FilterMaxLength"] = "256",
            ["Content:SubjectNameMaxLength"] = "100",
            ["Content:UnitNameMaxLength"] = "100",
            ["Content:LessonNameMaxLength"] = "100",
            ["Content:LessonExplanationMaxLength"] = "100000",
            ["Content:LessonSummaryMaxLength"] = "20000",
            ["Content:LessonObjectiveMaxLength"] = "300",
            ["Content:LessonObjectivesMaxCount"] = "20",
            ["Content:LessonVideoUrlMaxLength"] = "2048",
            ["Content:LessonImageMaxSizeInMb"] = "5",
            ["FileStorage:Provider"] = "Local",
            ["FileStorage:LocalRootPath"] = MediaRoot,
            ["FileStorage:PublicBaseUrl"] = "/api/media",
            ["CoreOtp:Secret"] = TestOtpSecret,
            ["Sms:Provider"] = "Fake",
            ["Auth:DisplayNameMaxLength"] = "100",
            ["Auth:EmailMaxLength"] = "256",
            ["Auth:RefreshTokenCookieName"] = "elmanhg_refresh",
            ["Auth:RefreshTokenCookiePath"] = "/api/auth",
            ["Auth:RefreshTokenCookieSecure"] = "true",
            ["Auth:OtpRequestPermitLimit"] = "1000",
            ["Auth:OtpRequestWindowSeconds"] = "600",
            ["Auth:CredentialPermitLimit"] = "1000",
            ["Auth:CredentialWindowSeconds"] = "60",
            ["IdentityOptions:User:RequireUniqueEmail"] = "false",
            ["IdentityOptions:Password:RequiredLength"] = "8",
            ["IdentityOptions:Password:RequireDigit"] = "true",
            ["IdentityOptions:Password:RequireLowercase"] = "false",
            ["IdentityOptions:Password:RequireUppercase"] = "false",
            ["IdentityOptions:Password:RequireNonAlphanumeric"] = "false",
            ["IdentityOptions:Password:RequiredUniqueChars"] = "1",
            ["IdentityOptions:Lockout:AllowedForNewUsers"] = "true",
            ["IdentityOptions:Lockout:MaxFailedAccessAttempts"] = "5",
            ["IdentityOptions:Lockout:DefaultLockoutTimeSpan"] = "00:15:00",
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISmsSender>();
            services.AddSingleton<ISmsSender>(Sms);
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
            services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(ApiFactory).Assembly));
        });
    }

    public new async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync();
        if (Directory.Exists(MediaRoot))
        {
            Directory.Delete(MediaRoot, recursive: true);
        }

        await base.DisposeAsync();
    }
}
