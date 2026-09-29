using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
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
    public async Task SaveChanges_DuplicatePaymobTransactionId_ThrowsTransactionAlreadyRecorded()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var transactionId = $"txn-{Guid.NewGuid():N}";
        var first = SubscriptionTestData.NewPayment(student.Id, 19900);
        first.MarkFailed(transactionId, "{}", DateTimeOffset.UtcNow);
        await SaveAsync(first);
        var second = SubscriptionTestData.NewPayment(student.Id, 19900);
        second.MarkFailed(transactionId, "{}", DateTimeOffset.UtcNow);

        var act = () => SaveAsync(second);

        var exception = (await act.Should().ThrowAsync<ConflictCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCodes.PaymentTransactionAlreadyRecorded);
        var postgres = exception.InnerException.Should().BeAssignableTo<DbUpdateException>().Which.InnerException.Should().BeOfType<PostgresException>().Which;
        (postgres.SqlState, postgres.ConstraintName).Should().Be((PostgresErrorCodes.UniqueViolation, AppDbContext.PaymobTransactionIndex));
    }

    [Fact]
    public async Task SaveChanges_StalePayment_ThrowsPaymentModifiedConcurrently()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = SubscriptionTestData.NewPayment(student.Id, 19900);
        await SaveAsync(payment);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstCopy = await firstContext.Payments.SingleAsync(x => x.Id == payment.Id, CancellationToken);
        var secondCopy = await secondContext.Payments.SingleAsync(x => x.Id == payment.Id, CancellationToken);
        firstCopy.MarkFailed($"txn-{Guid.NewGuid():N}", "{}", DateTimeOffset.UtcNow);
        await firstContext.SaveChangesAsync(CancellationToken);
        secondCopy.FlagForReview(PaymentReviewReason.AskTeacherWithoutBase);

        var act = () => secondContext.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentModifiedConcurrently);
    }

    [Fact]
    public async Task SaveChanges_StaleSubscription_ThrowsSubscriptionModifiedConcurrently()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var subscription = new SubscriptionBuilder().ForStudent(student.Id).StartingAt(DateTimeOffset.UtcNow).Build();
        await SubscriptionTestData.SeedSubscriptionAsync(factory, subscription, CancellationToken);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstCopy = await firstContext.Subscriptions.SingleAsync(x => x.Id == subscription.Id, CancellationToken);
        var secondCopy = await secondContext.Subscriptions.SingleAsync(x => x.Id == subscription.Id, CancellationToken);
        firstCopy.Cancel(DateTimeOffset.UtcNow);
        await firstContext.SaveChangesAsync(CancellationToken);
        secondCopy.MarkPastDue();

        var act = () => secondContext.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubscriptionModifiedConcurrently);
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
