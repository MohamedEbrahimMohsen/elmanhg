using Elmanhg.Domain.TrainingData;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.GradeReviews;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.TrainingData;

public sealed class EssayGradeTrainingExportPageTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetExportPageAsync_ReviewedGrade_ReturnsOnlyTeacherReviewedRow()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (reviewedId, _, _, _) = await GradeReviewTestData.SeedInReviewEssayAsync(factory, subjectId);
        var (unreviewedId, _, _, _) = await GradeReviewTestData.SeedInReviewEssayAsync(factory, subjectId);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var grade = await context.EssayGrades.SingleAsync(x => x.Id == reviewedId, CancellationToken);
            grade.Override(4m, Guid.NewGuid(), "Full marks for the definition.", DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(CancellationToken);
        }

        using var readScope = factory.Services.CreateScope();
        var repository = readScope.ServiceProvider.GetRequiredService<IEssayGradeTrainingRecordRepository>();
        var page = await repository.GetExportPageAsync(new TrainingRecordFilter(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), subjectId), null, 10, CancellationToken);

        page.Select(x => (x.EssayGradeId, x.Trigger)).Should().BeEquivalentTo([(reviewedId, EssayGradeTrainingTrigger.TeacherReviewed), (unreviewedId, EssayGradeTrainingTrigger.Completed)]);
    }
}
