namespace Elmanhg.Application.Shared.Storage;

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string key, CancellationToken cancellationToken);

    Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
