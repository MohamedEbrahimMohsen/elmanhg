using Elmanhg.Application.Shared.Observability;
using MediatR;

namespace Elmanhg.Application.Observability.ReportClientError;

public sealed record ReportClientErrorCommand(string Message, string? ErrorName, string? Stack, ClientErrorSource Source, string? Path) : IRequest;
