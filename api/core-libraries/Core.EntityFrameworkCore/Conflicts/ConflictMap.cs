using System.Diagnostics.CodeAnalysis;
using Core.Errors;
using Microsoft.EntityFrameworkCore;

namespace Core.EntityFrameworkCore.Conflicts;

public sealed class ConflictMap(Func<DbUpdateException, UniqueViolation?> detectUniqueViolation)
{
    private readonly List<(Type EntityType, string ErrorCode)> _concurrencyRules = [];
    private readonly List<(Func<UniqueViolation, bool> Matches, string ErrorCode)> _uniqueRules = [];

    public ConflictMap MapConcurrency<TEntity>(string errorCode) where TEntity : class
    {
        _concurrencyRules.Add((typeof(TEntity), errorCode));
        return this;
    }

    public ConflictMap MapUniqueConstraint(string constraintName, string errorCode)
    {
        _uniqueRules.Add((violation => string.Equals(violation.ConstraintName, constraintName, StringComparison.Ordinal), errorCode));
        return this;
    }

    public ConflictMap MapUniqueTable(string tableName, string errorCode)
    {
        _uniqueRules.Add((violation => string.Equals(violation.TableName, tableName, StringComparison.Ordinal), errorCode));
        return this;
    }

    public bool TryTranslate(DbUpdateException exception, [NotNullWhen(true)] out ConflictCoreException? conflict)
    {
        var errorCode = FindConcurrencyCode(exception) ?? FindUniqueCode(exception);
        conflict = errorCode is null ? null : new ConflictCoreException(errorCode, innerException: exception);
        return conflict is not null;
    }

    private string? FindConcurrencyCode(DbUpdateException exception)
    {
        if (exception is not DbUpdateConcurrencyException)
        {
            return null;
        }

        var entities = exception.Entries.Select(x => x.Entity).ToList();
        return _concurrencyRules.FirstOrDefault(rule => entities.Any(rule.EntityType.IsInstanceOfType)).ErrorCode;
    }

    private string? FindUniqueCode(DbUpdateException exception)
    {
        var violation = detectUniqueViolation(exception);
        return violation is null ? null : _uniqueRules.FirstOrDefault(rule => rule.Matches(violation)).ErrorCode;
    }
}
