using Core.Auditing;
using Core.CQRS;
using Core.EntityFrameworkCore;
using Core.Exceptions;
using Core.Identity;
using Core.Localization;
using Core.Logging;
using Core.OTP;
using Core.Utilities;
using DotNetEnv;
using Elmanhg.Api.Authorization;
using Elmanhg.Api.FileStorage;
using Elmanhg.Api.Hosting;
using Elmanhg.Api.RateLimiting;
using Elmanhg.Api.Realtime;
using Elmanhg.Api.Workers;
using Elmanhg.Application;
using Elmanhg.Application.Auth.SeedAdmin;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Infrastructure;
using Elmanhg.Infrastructure.Data.Context;
using MediatR;
using Pgvector.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

#region LOCAL ENVIRONMENT
if (builder.Environment.IsDevelopment())
{
    Env.TraversePath().Load();
    builder.Configuration.AddEnvironmentVariables();
}
#endregion

#region BUILD-TIME OPENAPI
// The build-time OpenAPI run starts this host with no deployment configuration: it takes the committed non-secret
// shapes so ValidateOnStart passes, and it must never touch the database through the admin seed.
var isBuildTimeOpenApiGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (isBuildTimeOpenApiGeneration)
{
    builder.Configuration.AddJsonFile("appsettings.example.json", optional: false);
    // The example leaves the student-id hash key empty, which Production refuses; this host hashes nothing, so a throwaway key is enough.
    builder.Configuration.AddInMemoryCollection([new("TrainingData:StudentIdHashKey", Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)))]);
}
#endregion

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Nullable enum use sites already carry null through oneOf; a null member inside the named component would make null a legal value everywhere.
builder.Services.AddOpenApi(options => options.AddSchemaTransformer((schema, _, _) =>
{
    if (schema.Enum is { Count: > 0 } members)
    {
        schema.Enum = [.. members.Where(x => x is not null && x.GetValueKind() != JsonValueKind.Null)];
    }

    return Task.CompletedTask;
}));

#region IDENTITY
builder.Services.AddCoreIdentity<User, Guid, Role, AppDbContext>(configuration: builder.Configuration, dbContextOptions: options => options.UseNpgsql(builder.Configuration.GetConnectionString("DbConnectionString"), npgsql => npgsql.EnableRetryOnFailure().UseVector()), identityOptions: options => builder.Configuration.GetSection(nameof(IdentityOptions)).Bind(options));
builder.Services.AddAuthorizationBuilder().AddPolicy(DefaultCodes.AuthenticatedUser, policy => policy.RequireAuthenticatedUser()).AddPermissionMatrixPolicies();
#endregion

#region CORE SERVICES
builder.Services.AddCoreLogging(builder.Configuration, builder.Environment);
builder.Services.AddCoreLocalization();
builder.Services.AddCoreExceptions();
// Auditing before CQRS so AuditBehaviour sits outermost in the MediatR pipeline.
builder.Services.AddCoreAuditing(builder.Configuration);
// After auditing (AuditBehaviour stays outermost), before CQRS so RequestMetricsBehaviour also sees validation failures.
builder.Services.AddElmanhgObservability(builder.Configuration, builder.Environment);
builder.Services.AddCoreCQRS();
builder.Services.AddCoreOtp(builder.Configuration);
builder.Services.AddCoreEntityFrameworkCore<User, Role, Guid, AppDbContext>();
builder.Services.AddCoreUtilities();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHostedService<ExpiredExamSubmissionWorker>();
builder.Services.AddHostedService<SubscriptionLapseWorker>();
builder.Services.AddHostedService<LessonContentIndexWorker>();
builder.Services.AddHostedService<TeacherVoiceTranscriptionWorker>();
builder.Services.AddHostedService<TeacherThreadSlaWorker>();
builder.Services.AddElmanhgRealtime();
builder.Services.AddAuthRateLimiting();
#endregion

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

#region DEPLOY-TIME MIGRATION
// The compose `migrate` service runs `--MigrateAndExit=true`: apply pending migrations, then exit before the seed and the HTTP pipeline.
if (MigrationCommand.IsRequested(app.Configuration))
{
    try
    {
        await MigrationCommand.RunAsync(app.Services, CancellationToken.None);
    }
    finally
    {
        await Serilog.Log.CloseAndFlushAsync();
    }

    return;
}
#endregion

#region LOAD-TEST SEED
// deploy/load-test.sh runs `migrate --SeedLoadTestAndExit=true`: seed the load-test curriculum and students, then exit before the HTTP pipeline.
if (LoadTestSeedCommand.IsRequested(app.Configuration))
{
    try
    {
        Environment.ExitCode = await LoadTestSeedCommand.RunAsync(app.Services, app.Configuration, app.Environment, CancellationToken.None);
    }
    finally
    {
        await Serilog.Log.CloseAndFlushAsync();
    }

    return;
}
#endregion

#region SEED
if (!isBuildTimeOpenApiGeneration)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SeedAdminCommand());
}
#endregion

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// First: every later middleware (logging, rate limits) must see the client IP and scheme Caddy forwarded.
app.UseReverseProxyForwardedHeaders();

// Right after forwarded headers, so every downstream middleware and handler resolves in the caller's language.
app.UseCoreLocalization(builder.Configuration);

app.UseMiddleware<CoreRequestLoggingMiddleware>();

app.UseHttpsRedirection();

app.UseMediaStorage();

app.UseAuthorization();

app.UseMiddleware<CoreExceptionMiddleware>();

app.UseRateLimiter();

app.MapControllers();

app.MapElmanhgRealtime();

app.MapHealthChecks("/health").AllowAnonymous();

try
{
    await app.RunAsync();
}
finally
{
    // Flush buffered sinks so the last batch reaches the log store on shutdown.
    await Serilog.Log.CloseAndFlushAsync();
}

public partial class Program;
