using System.Text.Json.Nodes;

namespace Core.Auditing;

public sealed record AuditValueChange(JsonNode? Before, JsonNode? After);
