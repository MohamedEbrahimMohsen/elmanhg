using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class QueryCachingOptions
{
    public const string SectionName = "Caching";

    [Range(0, 3600)]
    public int DefaultSeconds { get; set; } = 60;
}
