using Elmanhg.Application.Shared.RichText;
using Elmanhg.Infrastructure.Storage;
using Ganss.Xss;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.RichText;

public sealed class RichTextSanitizer : IRichTextSanitizer
{
    private static readonly string[] AllowedTags = ["p", "br", "strong", "em", "u", "s", "code", "pre", "blockquote", "h2", "h3", "ul", "ol", "li", "a", "hr", "img", "span", "div"];
    private static readonly string[] AllowedAttributes = ["href", "src", "alt", "start", "data-type", "data-latex"];
    private static readonly string[] AllowedSchemes = ["https", "http", "mailto"];
    private static readonly string[] UriAttributes = ["href", "src"];

    private readonly HtmlSanitizer _sanitizer;
    private readonly string _imageUrlPrefix;

    public RichTextSanitizer(IOptions<FileStorageOptions> fileStorageOptions)
    {
        _imageUrlPrefix = $"{fileStorageOptions.Value.PublicBaseUrl.TrimEnd('/')}/";
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        _sanitizer.AllowedTags.UnionWith(AllowedTags);
        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.UnionWith(AllowedAttributes);
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.UnionWith(AllowedSchemes);
        _sanitizer.UriAttributes.Clear();
        _sanitizer.UriAttributes.UnionWith(UriAttributes);
        _sanitizer.AllowedCssProperties.Clear();
        _sanitizer.FilterUrl += FilterImageSource;
    }

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return _sanitizer.Sanitize(html);
    }

    private void FilterImageSource(object? sender, FilterUrlEventArgs args)
    {
        if (string.Equals(args.Tag.NodeName, "IMG", StringComparison.OrdinalIgnoreCase) && !args.OriginalUrl.StartsWith(_imageUrlPrefix, StringComparison.Ordinal))
        {
            args.SanitizedUrl = null;
        }
    }
}
