using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace Elmanhg.Tests.Integration.Exams;

public static class MultiUnitExamTestData
{
    private const int DefaultMcqCount = 10;
    private static readonly string[] UnitNames = ["Mechanics", "Waves", "Optics", "Heat"];

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<(Guid SubjectId, List<Guid> UnitIds, List<List<Guid>> QuestionIds)> SeedMultiUnitSubjectAsync(ApiFactory factory, int[] mcqPerUnit, int? timeLimitMinutes = 30, int passMark = 50, bool unitBlueprints = true, bool subjectDefault = false)
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken).ConfigureAwait(false);
        List<Guid> unitIds = [];
        List<List<Guid>> questionIds = [];
        for (var index = 0; index < mcqPerUnit.Length; index++)
        {
            var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, UnitNames[index], index + 1, CancellationToken).ConfigureAwait(false);
            var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, $"{UnitNames[index]} basics", 1, LessonState.Published, CancellationToken).ConfigureAwait(false);
            List<Guid> unitQuestionIds = [];
            for (var count = 0; count < mcqPerUnit[index]; count++)
            {
                unitQuestionIds.Add(await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, CancellationToken).ConfigureAwait(false));
            }

            unitIds.Add(unitId);
            questionIds.Add(unitQuestionIds);
        }

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (unitBlueprints)
        {
            var units = await context.Units.Where(x => x.SubjectId == subjectId).ToListAsync(CancellationToken).ConfigureAwait(false);
            context.ExamBlueprints.AddRange(unitIds.Select((unitId, index) => ExamBlueprint.CreateForUnit(units.Single(x => x.Id == unitId), Shape(mcqPerUnit[index], timeLimitMinutes, passMark), ExamBlueprintBuilder.Plenty(), Guid.NewGuid())));
        }

        if (subjectDefault)
        {
            var subject = await context.Subjects.SingleAsync(x => x.Id == subjectId, CancellationToken).ConfigureAwait(false);
            context.ExamBlueprints.Add(ExamBlueprint.CreateForSubject(subject, Shape(DefaultMcqCount, timeLimitMinutes, passMark), ExamBlueprintBuilder.Plenty(), Guid.NewGuid()));
        }

        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return (subjectId, unitIds, questionIds);
    }

    public static Task<HttpResponseMessage> StartMultiAsync(HttpClient client, Guid subjectId, IEnumerable<Guid> unitIds, int size)
    {
        return client.PostAsJsonAsync($"{ExamTestData.Route}/subjects/{subjectId}/multi-unit", new { unitIds, size }, CancellationToken);
    }

    public static string PreviewUrl(Guid subjectId, IEnumerable<Guid> unitIds, int size) => $"{ExamTestData.Route}/subjects/{subjectId}/multi-unit/preview?{string.Concat(unitIds.Select(x => $"unitIds={x}&"))}size={size}";

    private static ExamBlueprintShape Shape(int mcqCount, int? timeLimitMinutes, int passMark) => new([new ExamTypeCount(QuestionType.Mcq, mcqCount)], null, timeLimitMinutes, passMark);
}
