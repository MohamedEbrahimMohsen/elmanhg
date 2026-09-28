using Elmanhg.Application.Shared.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Storage;

public sealed class LocalDiskFileStorage(IOptions<FileStorageOptions> fileStorageOptions, IHostEnvironment hostEnvironment) : IFileStorage
{
    public async Task<string> SaveAsync(Stream content, string key, CancellationToken cancellationToken)
    {
        var options = fileStorageOptions.Value;
        var root = Path.GetFullPath(options.LocalRootPath, hostEnvironment.ContentRootPath);
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("The storage key escapes the storage root.", nameof(key));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
        await using var file = new FileStream(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous });
        await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

        return $"{options.PublicBaseUrl.TrimEnd('/')}/{key}";
    }
}
