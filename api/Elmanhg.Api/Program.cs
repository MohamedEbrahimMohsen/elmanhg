using Core.Auditing;
using Core.CQRS;
using Core.EntityFrameworkCore;
using Core.Exceptions;
using Core.Identity;
using Core.Localization;
using Core.Logging;
using Core.Utilities;
using DotNetEnv;
using Elmanhg.Application;
using Elmanhg.Domain.Identity;
using Elmanhg.Infrastructure;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

#region LOCAL ENVIRONMENT
if (builder.Environment.IsDevelopment())
{
    Env.TraversePath().Load();
    builder.Configuration.AddEnvironmentVariables();
}
#endregion

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

#region IDENTITY
builder.Services.AddCoreIdentity<User, Guid, Role, AppDbContext>(configuration: builder.Configuration, dbContextOptions: options => options.UseNpgsql(builder.Configuration.GetConnectionString("DbConnectionString"), npgsql => npgsql.EnableRetryOnFailure()));
#endregion

#region CORE SERVICES
builder.Services.AddCoreLogging(builder.Configuration, builder.Environment);
builder.Services.AddCoreLocalization();
builder.Services.AddCoreExceptions();
// Auditing before CQRS so AuditBehaviour sits outermost in the MediatR pipeline.
builder.Services.AddCoreAuditing(builder.Configuration);
builder.Services.AddCoreCQRS();
builder.Services.AddCoreEntityFrameworkCore<User, Role, Guid, AppDbContext>();
builder.Services.AddCoreUtilities();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
#endregion

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

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
