using Microsoft.AspNetCore.Http;

namespace Core.Storage.Media;

public sealed record PublicMediaOptions(PathString RequestPath, IReadOnlyList<string> PrivateFolders);
