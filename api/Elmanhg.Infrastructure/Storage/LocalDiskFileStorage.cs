using Elmanhg.Application.Shared.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Storage;

public sealed class LocalDiskFileStorage(IOptions<FileStorageOptions> fileStorageOptions, IHostEnvironment hostEnvironment) : IFileStorage
{
    public async Task<string> SaveAsync(Stream content, string key, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous });
        await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

        return $"{fileStorageOptions.Value.PublicBaseUrl.TrimEnd('/')}/{key}";
    }

    public Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            return Task.FromResult<StoredFile?>(null);
        }

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        return Task.FromResult<StoredFile?>(new StoredFile(stream, stream.Length, MediaContentTypes.FromKey(key)));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        var root = Path.GetFullPath(fileStorageOptions.Value.LocalRootPath, hostEnvironment.ContentRootPath);
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("The storage key escapes the storage root.", nameof(key));
        }

        return path;
    }
}
