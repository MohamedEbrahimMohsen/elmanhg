namespace Core.Storage.Media;

public static class MediaCacheControl
{
    // Public media is stored under write-once random keys, so a public URL never serves different bytes.
    public const string PublicImmutable = "public, max-age=31536000, immutable";

    public const string PrivateNoStore = "private, no-store";
}
