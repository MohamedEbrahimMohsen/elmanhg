namespace Core.EntityFrameworkCore.Conflicts;

public sealed record UniqueViolation(string? ConstraintName, string? TableName);
