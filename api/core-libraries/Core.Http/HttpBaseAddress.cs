namespace Core.Http;

public static class HttpBaseAddress
{
    public static Uri From(string baseUrl) => new(baseUrl.TrimEnd('/') + "/");

    public static string Combine(string baseUrl, string relativePath) => $"{baseUrl.TrimEnd('/')}/{relativePath}";
}
