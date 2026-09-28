using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class ServableQuestionSpecificationPersistenceTests(ApiFactory factory)
{
    [Fact]
    public async Task WhereServable_MixedStatesAndLessonStates_ReturnsOnlyApprovedUnretiredInPublished()
    {
        var seed = await SeedAsync();
        List<Guid> lessonIds = [seed.PublishedLessonId, seed.DraftLessonId, seed.ArchivedLessonId];
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var result = await context.Questions
            .WhereServable(context.Lessons)
            .Where(x => lessonIds.Contains(x.LessonId))
            .Select(x => x.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        result.Should().Equal(seed.ServableQuestionId);
    }

    [Fact]
    public async Task CountServableByLessonAsync_MixedLessons_CountsOnlyPublished()
    {
        var seed = await SeedAsync();
        using var scope = factory.Services.CreateScope();
        var questionRepository = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();

        var result = await questionRepository.CountServableByLessonAsync([seed.PublishedLessonId, seed.DraftLessonId, seed.ArchivedLessonId], TestContext.Current.CancellationToken);

        result.Should().Equal(new Dictionary<Guid, int> { [seed.PublishedLessonId] = 1 });
    }

    [Fact]
    public async Task GetServableIdsInLessonAsync_MixedStates_ReturnsOnlyServableInLesson()
    {
        var seed = await SeedAsync();
        using var scope = factory.Services.CreateScope();
        var questionRepository = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();

        var result = await questionRepository.GetServableIdsInLessonAsync(seed.PublishedLessonId, TestContext.Current.CancellationToken);

        result.Should().Equal(seed.ServableQuestionId);
    }

    private async Task<(Guid PublishedLessonId, Guid DraftLessonId, Guid ArchivedLessonId, Guid ServableQuestionId)> SeedAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, cancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken).ConfigureAwait(false);
        var publishedLessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Published, cancellationToken).ConfigureAwait(false);
        var draftLessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Momentum", 2, LessonState.Draft, cancellationToken).ConfigureAwait(false);
        var archivedLessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Energy", 3, LessonState.Archived, cancellationToken).ConfigureAwait(false);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken).ConfigureAwait(false);

        var servableQuestionId = await QuestionTestData.SeedQuestionAsync(factory, publishedLessonId, approved: true, cancellationToken).ConfigureAwait(false);
        await QuestionTestData.SeedQuestionAsync(factory, publishedLessonId, approved: false, cancellationToken).ConfigureAwait(false);
        await QuestionTestData.SeedRejectedQuestionAsync(factory, publishedLessonId, teacher, "Wrong unit", cancellationToken).ConfigureAwait(false);
        await QuestionTestData.SeedRetiredQuestionAsync(factory, publishedLessonId, cancellationToken).ConfigureAwait(false);
        await QuestionTestData.SeedQuestionAsync(factory, draftLessonId, approved: true, cancellationToken).ConfigureAwait(false);
        await QuestionTestData.SeedQuestionAsync(factory, archivedLessonId, approved: true, cancellationToken).ConfigureAwait(false);
        return (publishedLessonId, draftLessonId, archivedLessonId, servableQuestionId);
    }
}
