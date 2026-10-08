namespace Core.Spreadsheets;

internal sealed record OpcRelationship(string Id, string Type, string Target, bool IsExternal);
