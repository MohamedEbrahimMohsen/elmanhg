using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class LessonOpeningPersistenceTests(ApiFactory factory)
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChangesAsync_DuplicateOpening_ThrowsLessonAlreadyOpened()
    {
        var (studentId, lessonId) = await SeedAsync();
        await AddAsync(studentId, lessonId, T0);

        var act = () => AddAsync(studentId, lessonId, T0.AddMinutes(1));

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonAlreadyOpened);
    }

    private async Task<(Guid StudentId, Guid LessonId)> SeedAsync()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken).ConfigureAwait(false);
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Energy", 1, LessonState.Published, CancellationToken).ConfigureAwait(false);
        return (student.Id, lessonId);
    }

    private async Task AddAsync(Guid studentId, Guid lessonId, DateTimeOffset openedAt)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.AsNoTracking().SingleAsync(x => x.Id == lessonId, CancellationToken).ConfigureAwait(false);
        context.LessonOpenings.Add(LessonOpening.Record(studentId, lesson, openedAt));
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
    }
}
