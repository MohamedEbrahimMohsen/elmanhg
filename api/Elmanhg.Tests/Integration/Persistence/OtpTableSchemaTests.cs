using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class OtpTableSchemaTests(ApiFactory factory)
{
    [Fact]
    public async Task Migrate_FreshDatabase_OtpsHasNoRequestIpOrUserAgentColumn()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = await context.Database
            .SqlQuery<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = {"Otps"}")
            .ToListAsync(TestContext.Current.CancellationToken);

        columns.Should().Contain("PhoneNumber");
        columns.Should().NotContain("RequestIP").And.NotContain("UserAgent");
    }
}
