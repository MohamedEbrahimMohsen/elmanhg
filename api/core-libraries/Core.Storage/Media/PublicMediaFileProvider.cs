using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Core.Storage.Media;

public sealed class PublicMediaFileProvider(PhysicalFileProvider files, IReadOnlyList<string> privateFolders) : IFileProvider
{
    public IFileInfo GetFileInfo(string subpath)
    {
        // Windows 8.3 short names (TEACHE~1) would alias a private folder past the path check below.
        if (subpath.Contains('~'))
        {
            return new NotFoundFileInfo(subpath);
        }

        var file = files.GetFileInfo(subpath);
        return file.PhysicalPath is { } path && IsPrivate(path) ? new NotFoundFileInfo(subpath) : file;
    }

    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => files.Watch(filter);

    private bool IsPrivate(string path) => privateFolders.Any(folder => path.StartsWith(Path.Combine(files.Root, folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
}
