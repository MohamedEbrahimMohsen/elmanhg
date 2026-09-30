using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.EssayGrading;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Elmanhg.Tests.Integration.TrainingData.TrainingDataTestData;

namespace Elmanhg.Tests.Integration.TrainingData;

public sealed class EssayGradeTrainingRecordTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GradeEssay_StudentSession_WritesEssayGradeTrainingRecord()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (gradeId, _, questionId) = await EssayGradingTestData.SeedPendingAsync(factory, student.Id);

        await EssayGradingTestData.GradeAsync(factory, gradeId);

        var grade = await EssayGradingTestData.ReadAsync(factory, gradeId);
        var record = (await ReadRecordsAsync(gradeId)).Should().ContainSingle().Subject;
        (record.StudentHash, record.QuestionId, record.Outcome, record.OccurredAt).Should().Be((ExpectedHash(student.Id), questionId, EssayGradeStatus.Graded, grade.GradedAt!.Value));
        (record.Score, record.Confidence, record.Model, record.Criteria).Should().Be((grade.Score!.Value, grade.Confidence!.Value, grade.Model!, grade.Criteria!));
    }

    [Fact]
    public async Task GradeEssay_TestModeSession_WritesNoRecord()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (gradeId, _, _) = await EssayGradingTestData.SeedPendingAsync(factory, student.Id, isTestMode: true);

        await EssayGradingTestData.GradeAsync(factory, gradeId);

        (await EssayGradingTestData.ReadAsync(factory, gradeId)).Status.Should().Be(EssayGradeStatus.Graded);
        (await ReadRecordsAsync(gradeId)).Should().BeEmpty();
    }

    private async Task<List<EssayGradeTrainingRecord>> ReadRecordsAsync(Guid gradeId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().EssayGradeTrainingRecords.AsNoTracking().Where(x => x.EssayGradeId == gradeId).ToListAsync(CancellationToken).ConfigureAwait(false);
    }
}
