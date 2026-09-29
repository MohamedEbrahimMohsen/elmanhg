using Elmanhg.Application.Shared.Observability;

namespace Elmanhg.Api.Controllers.Observability;

public sealed record ReportClientErrorRequest(string Message, string? ErrorName, string? Stack, ClientErrorSource Source, string? Path);
