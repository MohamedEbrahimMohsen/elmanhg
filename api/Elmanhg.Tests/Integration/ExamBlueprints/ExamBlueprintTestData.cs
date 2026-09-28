using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.ExamBlueprints;

public static class ExamBlueprintTestData
{
    public static async Task<Guid> SeedServableMcqAsync(ApiFactory factory, Guid unitId, int count, CancellationToken cancellationToken)
    {
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Servable lesson", 1, LessonState.Published, cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < count; index++)
        {
            await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken).ConfigureAwait(false);
        }

        return lessonId;
    }

    public static async Task<List<ExamBlueprint>> ReadBlueprintsAsync(ApiFactory factory, Guid subjectId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.ExamBlueprints.IgnoreQueryFilters().AsNoTracking().Where(x => x.SubjectId == subjectId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
