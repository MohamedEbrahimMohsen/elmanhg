using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class PaymentPersistenceTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_DuplicatePaymobTransactionId_ThrowsUniqueViolation()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var transactionId = $"txn-{Guid.NewGuid():N}";
        var first = SubscriptionTestData.NewPayment(student.Id, 19900);
        first.MarkFailed(transactionId, "{}", DateTimeOffset.UtcNow);
        await SaveAsync(first);
        var second = SubscriptionTestData.NewPayment(student.Id, 19900);
        second.MarkFailed(transactionId, "{}", DateTimeOffset.UtcNow);

        var act = () => SaveAsync(second);

        var exception = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        var postgres = exception.InnerException.Should().BeOfType<PostgresException>().Which;
        (postgres.SqlState, postgres.ConstraintName).Should().Be((PostgresErrorCodes.UniqueViolation, AppDbContext.PaymobTransactionIndex));
    }

    [Fact]
    public async Task SaveChanges_PendingPaymentsWithoutTransactionId_AreAllowed()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);

        await SubscriptionTestData.SeedPendingPaymentAsync(factory, student.Id, CancellationToken);
        await SubscriptionTestData.SeedPendingPaymentAsync(factory, student.Id, CancellationToken);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.Payments.CountAsync(x => x.StudentId == student.Id, CancellationToken)).Should().Be(2);
    }

    private async Task SaveAsync(Payment payment)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Payments.Add(payment);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
    }
}
