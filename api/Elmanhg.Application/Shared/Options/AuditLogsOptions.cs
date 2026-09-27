using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class AuditLogsOptions
{
    public const string SectionName = "AuditLogs";

    [Range(1, int.MaxValue)]
    public int MaxPageSize { get; set; }

    [Range(1, int.MaxValue)]
    public int FilterMaxLength { get; set; }
}
