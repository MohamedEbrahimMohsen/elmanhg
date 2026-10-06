using System.ComponentModel.DataAnnotations;

namespace Core.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    [Required]
    public FileStorageProvider? Provider { get; set; }

    [Required]
    public string LocalRootPath { get; set; } = string.Empty;

    [Required]
    public string PublicBaseUrl { get; set; } = string.Empty;

    public string S3ServiceUrl { get; set; } = string.Empty;

    public string S3Region { get; set; } = "auto";

    public string S3BucketName { get; set; } = string.Empty;

    public string S3AccessKeyId { get; set; } = string.Empty;

    public string S3SecretAccessKey { get; set; } = string.Empty;

    public bool S3ForcePathStyle { get; set; } = true;

    public string GetPublicUrl(string key) => $"{PublicBaseUrl.TrimEnd('/')}/{key}";

    public string ResolveLocalRoot(string contentRootPath) => Path.GetFullPath(LocalRootPath, contentRootPath);
}
