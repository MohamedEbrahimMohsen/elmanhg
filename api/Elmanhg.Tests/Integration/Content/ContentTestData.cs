using Core.Auditing.Entities;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Content;

public static class ContentTestData
{
    public static async Task<Guid> SeedSubjectAsync(ApiFactory factory, string name, int order, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subject = Subject.Create(name, order, Guid.NewGuid());
        context.Subjects.Add(subject);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return subject.Id;
    }

    public static async Task<Guid> SeedUnitAsync(ApiFactory factory, Guid subjectId, string name, int order, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subject = await context.Subjects.SingleAsync(x => x.Id == subjectId, cancellationToken).ConfigureAwait(false);
        var unit = CurriculumUnit.Create(subject, name, order, Guid.NewGuid());
        context.Units.Add(unit);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return unit.Id;
    }

    public static async Task<int> ReadOrderAsync(ApiFactory factory, Guid subjectOrUnitId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subject = await context.Subjects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == subjectOrUnitId, cancellationToken).ConfigureAwait(false);
        if (subject is not null)
        {
            return subject.Order;
        }

        var unit = await context.Units.AsNoTracking().SingleAsync(x => x.Id == subjectOrUnitId, cancellationToken).ConfigureAwait(false);
        return unit.Order;
    }

    public static async Task<AuditLog> ReadAuditAsync(ApiFactory factory, string action, Guid resourceId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.AuditLogs.AsNoTracking().SingleAsync(x => x.Action == action && x.ResourceId == resourceId, cancellationToken).ConfigureAwait(false);
    }
}
