namespace Elmanhg.Application.Shared.Storage;

public sealed record StoredFile(Stream Content, long Length, string ContentType) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
