using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.GradeReviews;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class GradeConcurrencyTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_StaleEssayGrade_ThrowsGradeModifiedConcurrently(bool gradingFailed)
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (gradeId, _, _, _) = await GradeReviewTestData.SeedInReviewEssayAsync(factory, subjectId, gradingFailed);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstGrade = await first.EssayGrades.SingleAsync(x => x.Id == gradeId, CancellationToken);
        var secondGrade = await second.EssayGrades.SingleAsync(x => x.Id == gradeId, CancellationToken);
        firstGrade.Override(4m, Guid.NewGuid(), "First teacher.", DateTimeOffset.UtcNow);
        await first.SaveChangesAsync(CancellationToken);
        secondGrade.Override(3m, Guid.NewGuid(), "Second teacher.", DateTimeOffset.UtcNow);

        var act = () => second.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.GradeModifiedConcurrently);
    }
}
