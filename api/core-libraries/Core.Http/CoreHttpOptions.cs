namespace Core.Http;

public sealed class CoreHttpOptions
{
    public const string SectionName = "CoreHttp";

    public string UserAgent { get; set; } = string.Empty;
}
