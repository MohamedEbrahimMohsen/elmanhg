using Core.Auditing;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AuditChangeCaptureTests(ApiFactory factory)
{
    [Fact]
    public async Task SaveChangesAsync_AuditedEntityAdded_RecordsCreatedChange()
    {
        var (teacherId, subjectId) = await SeedPairAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var teacherSubject = await CreateTeacherSubjectAsync(context, teacherId, subjectId);
        context.TeacherSubjects.Add(teacherSubject);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var change = scope.ServiceProvider.GetRequiredService<IAuditChangeCollector>().Changes.Should().ContainSingle().Subject;
        change.EntityType.Should().Be(nameof(TeacherSubject));
        change.EntityId.Should().Be(teacherSubject.Id);
        change.Change.Should().Be(AuditChangeKind.Created);
        change.Properties.Keys.Should().BeEquivalentTo([nameof(TeacherSubject.TeacherId), nameof(TeacherSubject.SubjectId)]);
        change.Properties[nameof(TeacherSubject.TeacherId)].After!.ToJsonString().Should().Be($"\"{teacherId}\"");
    }

    [Fact]
    public async Task SaveChangesAsync_AuditedEntitySoftDeleted_RecordsDeletedChangeWithIsDeleted()
    {
        var (teacherId, subjectId) = await SeedPairAsync();
        await ScopeTestData.AssignAsync(factory, teacherId, subjectId, TestContext.Current.CancellationToken);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var teacherSubject = await context.TeacherSubjects.SingleAsync(x => x.TeacherId == teacherId && x.SubjectId == subjectId, TestContext.Current.CancellationToken);
        teacherSubject.Unassign(Guid.NewGuid());

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var change = scope.ServiceProvider.GetRequiredService<IAuditChangeCollector>().Changes.Should().ContainSingle().Subject;
        change.Change.Should().Be(AuditChangeKind.Deleted);
        change.Properties.Keys.Should().BeEquivalentTo([nameof(TeacherSubject.IsDeleted)]);
        change.Properties[nameof(TeacherSubject.IsDeleted)].Before!.ToJsonString().Should().Be("false");
        change.Properties[nameof(TeacherSubject.IsDeleted)].After!.ToJsonString().Should().Be("true");
    }

    [Fact]
    public async Task SaveChangesAsync_NonAuditedEntity_RecordsNothing()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Subjects.Add(Subject.Create("Chemistry", Guid.NewGuid()));

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        scope.ServiceProvider.GetRequiredService<IAuditChangeCollector>().Changes.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_SaveFails_RecordsNothing()
    {
        var (teacherId, subjectId) = await SeedPairAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.TeacherSubjects.Add(await CreateTeacherSubjectAsync(context, teacherId, subjectId));
        context.TeacherSubjects.Add(await CreateTeacherSubjectAsync(context, teacherId, subjectId));

        var act = () => context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
        scope.ServiceProvider.GetRequiredService<IAuditChangeCollector>().Changes.Should().BeEmpty();
    }

    private async Task<(Guid TeacherId, Guid SubjectId)> SeedPairAsync()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (teacher.Id, subjectId);
    }

    private static async Task<TeacherSubject> CreateTeacherSubjectAsync(AppDbContext context, Guid teacherId, Guid subjectId)
    {
        var teacher = await context.Users.SingleAsync(x => x.Id == teacherId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var subject = await context.Subjects.SingleAsync(x => x.Id == subjectId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return TeacherSubject.Create(teacher, subject, Guid.NewGuid());
    }
}
