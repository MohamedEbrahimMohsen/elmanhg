using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.ExamBlueprints;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class ExamBlueprintPersistenceTests(ApiFactory factory)
{
    private static readonly ExamBlueprintShape Shape = ExamBlueprintBuilder.Shape(new ExamTypeCount(QuestionType.Mcq, 1));

    [Fact]
    public async Task SaveChanges_SecondSubjectDefault_ThrowsModifiedConcurrently()
    {
        var (subjectId, _) = await SeedAsync();
        await AddAsync(context => CreateDefaultAsync(context, subjectId));

        var act = () => AddAsync(context => CreateDefaultAsync(context, subjectId));

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ExamBlueprintModifiedConcurrently);
    }

    [Fact]
    public async Task SaveChanges_SecondBlueprintForUnit_ThrowsModifiedConcurrently()
    {
        var (_, unitId) = await SeedAsync();
        await AddAsync(context => CreateForUnitAsync(context, unitId));

        var act = () => AddAsync(context => CreateForUnitAsync(context, unitId));

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ExamBlueprintModifiedConcurrently);
    }

    [Fact]
    public async Task SaveChanges_UnitBlueprintAfterDelete_Succeeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, unitId) = await SeedAsync();
        var first = await AddAsync(context => CreateForUnitAsync(context, unitId));
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await context.ExamBlueprints.SingleAsync(x => x.Id == first.Id, cancellationToken);
            stored.Delete(Guid.NewGuid());
            await context.SaveChangesAsync(cancellationToken);
        }

        await AddAsync(context => CreateForUnitAsync(context, unitId));

        var rows = await ExamBlueprintTestData.ReadBlueprintsAsync(factory, subjectId, cancellationToken);
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(x => !x.IsDeleted);
    }

    private async Task<(Guid SubjectId, Guid UnitId)> SeedAsync()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (subjectId, unitId);
    }

    private static async Task<ExamBlueprint> CreateDefaultAsync(AppDbContext context, Guid subjectId)
    {
        var subject = await context.Subjects.AsNoTracking().SingleAsync(x => x.Id == subjectId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return ExamBlueprint.CreateForSubject(subject, Shape, ExamBlueprintBuilder.Plenty(), Guid.NewGuid());
    }

    private static async Task<ExamBlueprint> CreateForUnitAsync(AppDbContext context, Guid unitId)
    {
        var unit = await context.Units.AsNoTracking().SingleAsync(x => x.Id == unitId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return ExamBlueprint.CreateForUnit(unit, Shape, ExamBlueprintBuilder.Plenty(), Guid.NewGuid());
    }

    private async Task<ExamBlueprint> AddAsync(Func<AppDbContext, Task<ExamBlueprint>> create)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var blueprint = await create(context).ConfigureAwait(false);
        context.ExamBlueprints.Add(blueprint);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return blueprint;
    }
}
