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
using Elmanhg.Api.RateLimiting;
using Elmanhg.Application;
using Elmanhg.Application.Auth.SeedAdmin;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Infrastructure;
using Elmanhg.Infrastructure.Data.Context;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Reflection;
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
}
#endregion

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

#region IDENTITY
builder.Services.AddCoreIdentity<User, Guid, Role, AppDbContext>(configuration: builder.Configuration, dbContextOptions: options => options.UseNpgsql(builder.Configuration.GetConnectionString("DbConnectionString"), npgsql => npgsql.EnableRetryOnFailure()), identityOptions: options => builder.Configuration.GetSection(nameof(IdentityOptions)).Bind(options));
builder.Services.AddAuthorizationBuilder().AddPolicy(DefaultCodes.AuthenticatedUser, policy => policy.RequireAuthenticatedUser());
#endregion

#region CORE SERVICES
builder.Services.AddCoreLogging(builder.Configuration, builder.Environment);
builder.Services.AddCoreLocalization();
builder.Services.AddCoreExceptions();
// Auditing before CQRS so AuditBehaviour sits outermost in the MediatR pipeline.
builder.Services.AddCoreAuditing(builder.Configuration);
builder.Services.AddCoreCQRS();
builder.Services.AddCoreOtp(builder.Configuration);
builder.Services.AddCoreEntityFrameworkCore<User, Role, Guid, AppDbContext>();
builder.Services.AddCoreUtilities();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddAuthRateLimiting();
#endregion

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

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

// First, so every downstream middleware and handler resolves in the caller's language.
app.UseCoreLocalization(builder.Configuration);

app.UseMiddleware<CoreRequestLoggingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseMiddleware<CoreExceptionMiddleware>();

app.UseRateLimiter();

app.MapControllers();

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
