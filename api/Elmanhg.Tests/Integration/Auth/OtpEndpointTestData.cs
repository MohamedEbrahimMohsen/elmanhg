using Core.OTP.Entities;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Auth;

public static class OtpEndpointTestData
{
    public const string SendRoute = "/api/auth/otp/send";
    public const string VerifyRoute = "/api/auth/otp/verify";

    public static async Task<(HttpStatusCode Status, string? Code)> PostAsync(HttpClient client, string route, object body, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(route, body, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
        return (response.StatusCode, json.TryGetProperty("code", out var code) ? code.GetString() : null);
    }

    public static async Task SeedAsync(ApiFactory factory, Otp otp, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Otps.Add(otp);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<Otp> ReadOtpAsync(ApiFactory factory, string phone, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Otps.AsNoTracking().SingleAsync(x => x.Recipient == phone, cancellationToken).ConfigureAwait(false);
    }
}
