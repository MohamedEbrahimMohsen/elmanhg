using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Core.Persistence;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OtpErrorCodes = Core.OTP.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class OtpConflictMapTests(ApiFactory factory)
{
    [Fact]
    public void TryTranslate_OtpConcurrency_ReturnsOtpModifiedConcurrently()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exception = ConcurrencyFailures.For(context, new OtpBuilder().Build());

        var translated = AppDbContext.Conflicts.TryTranslate(exception, out var conflict);

        translated.Should().BeTrue();
        conflict!.ErrorCode.Should().Be(OtpErrorCodes.OtpModifiedConcurrently);
        conflict.StatusCode.Should().Be(409);
        conflict.InnerException.Should().BeSameAs(exception);
    }

    [Fact]
    public void TryTranslate_OtpRecipientUniqueViolation_IsNotTranslated()
    {
        var exception = new DbUpdateException("duplicate", new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: AppDbContext.OtpRecipientIndex));

        var translated = AppDbContext.Conflicts.TryTranslate(exception, out var conflict);

        translated.Should().BeFalse();
        conflict.Should().BeNull();
    }
}
