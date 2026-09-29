using Core.Auditing;
using Core.DDD.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Core.EntityFrameworkCore.Auditing;

public static class AuditChangeReader
{
    public const string ExcludedAnnotation = "Core:AuditExcluded";

    // Bookkeeping stamped on every write; the audit row already carries actor and time.
    private static readonly HashSet<string> IgnoredProperties = [nameof(IEntity.Id), nameof(IEntity.DeletedAt), nameof(IAuditEntity.CreatedBy), nameof(IAuditEntity.CreationDate), nameof(IAuditEntity.UpdatedBy), nameof(IAuditEntity.UpdationDate)];

    public static List<AuditEntityChange> Read(ChangeTracker changeTracker)
    {
        return changeTracker.Entries()
            .Where(entry => entry.Entity is IAuditedEntity && entry.Entity is IEntity && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(ToChange)
            .Where(change => change.Properties.Count > 0)
            .ToList();
    }

    private static AuditEntityChange ToChange(EntityEntry entry)
    {
        var kind = ToKind(entry);
        var properties = entry.Properties
            .Where(property => IsAudited(entry.State, property))
            .ToDictionary(property => property.Metadata.Name, property => new AuditValueChange(entry.State == EntityState.Added ? null : Serialize(property.OriginalValue), entry.State == EntityState.Deleted ? null : Serialize(property.CurrentValue)));

        return new AuditEntityChange(entry.Metadata.ClrType.Name, ((IEntity)entry.Entity).Id, kind, properties);
    }

    private static AuditChangeKind ToKind(EntityEntry entry)
    {
        return entry.State switch
        {
            EntityState.Added => AuditChangeKind.Created,
            EntityState.Deleted => AuditChangeKind.Deleted,
            _ when IsSoftDeleted(entry) => AuditChangeKind.Deleted,
            _ => AuditChangeKind.Modified,
        };
    }

    private static bool IsSoftDeleted(EntityEntry entry)
    {
        var isDeleted = entry.Property(nameof(IEntity.IsDeleted));
        return isDeleted.IsModified && isDeleted.CurrentValue is true;
    }

    private static bool IsAudited(EntityState state, PropertyEntry property)
    {
        if (IgnoredProperties.Contains(property.Metadata.Name) || property.Metadata.IsShadowProperty() || property.Metadata.IsConcurrencyToken || property.Metadata.FindAnnotation(ExcludedAnnotation)?.Value is true)
        {
            return false;
        }

        return state is EntityState.Modified
            ? property.IsModified && !Equals(property.OriginalValue, property.CurrentValue)
            : property.Metadata.Name != nameof(IEntity.IsDeleted);
    }

    private static JsonNode? Serialize(object? value) => value is null ? null : JsonSerializer.SerializeToNode(value, AuditDiff.SerializerOptions);
}
