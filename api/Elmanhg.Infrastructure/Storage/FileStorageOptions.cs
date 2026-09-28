using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    [Required]
    public FileStorageProvider? Provider { get; set; }

    [Required]
    public string LocalRootPath { get; set; } = string.Empty;

    [Required]
    public string PublicBaseUrl { get; set; } = string.Empty;
}
