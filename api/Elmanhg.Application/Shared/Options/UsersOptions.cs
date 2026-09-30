using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class UsersOptions
{
    public const string SectionName = "Users";

    [Range(1, 100)]
    public int ListMaxPageSize { get; set; } = 100;

    [Range(1, 256)]
    public int SearchMaxLength { get; set; } = 256;

    [Range(0, 300)]
    public int ActiveStatusCacheSeconds { get; set; } = 30;
}
