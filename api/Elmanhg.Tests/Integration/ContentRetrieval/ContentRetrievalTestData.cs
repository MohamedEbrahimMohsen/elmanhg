using Elmanhg.Application.ContentRetrieval.ReindexLessonContent;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.ContentRetrieval;

public static class ContentRetrievalTestData
{
    public static async Task<Guid> SeedPublishedLessonAsync(ApiFactory factory, string explanationHtml, string summaryHtml, IReadOnlyList<string> objectives, CancellationToken cancellationToken)
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, cancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Electricity", 1, cancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Ohm's law", 1, [], cancellationToken).ConfigureAwait(false);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.Include(x => x.Objectives).SingleAsync(x => x.Id == lessonId, cancellationToken).ConfigureAwait(false);
        var editor = Guid.NewGuid();
        lesson.Update(lesson.Name, explanationHtml, summaryHtml, null, objectives.Select(x => new LessonObjectiveContent(null, x)).ToList(), editor);
        lesson.Publish(editor);
        lesson.ClearDomainEvents();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return lessonId;
    }

    public static async Task ReindexAsync(ApiFactory factory, Guid lessonId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ReindexLessonContentCommand(lessonId), cancellationToken).ConfigureAwait(false);
    }

    public static async Task<List<LessonContentChunk>> ReadChunksAsync(ApiFactory factory, Guid lessonId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.LessonContentChunks.Where(x => x.LessonId == lessonId).AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<LessonContentIndex?> ReadIndexAsync(ApiFactory factory, Guid lessonId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.LessonContentIndexes.AsNoTracking().SingleOrDefaultAsync(x => x.LessonId == lessonId, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<List<Guid>> ReadStaleIdsAsync(ApiFactory factory, IReadOnlyCollection<Guid> excludedIds, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ILessonContentIndexRepository>().GetStaleLessonIdsAsync(excludedIds, 100000, cancellationToken).ConfigureAwait(false);
    }

    public static async Task RetireQuestionAsync(ApiFactory factory, Guid questionId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var question = await context.Questions.SingleAsync(x => x.Id == questionId, cancellationToken).ConfigureAwait(false);
        question.Retire(Guid.NewGuid());
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task TouchLessonAsync(ApiFactory factory, Guid lessonId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.SingleAsync(x => x.Id == lessonId, cancellationToken).ConfigureAwait(false);
        lesson.MoveTo(lesson.Order + 1, Guid.NewGuid());
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
