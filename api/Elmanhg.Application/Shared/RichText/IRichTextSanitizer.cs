namespace Elmanhg.Application.Shared.RichText;

public interface IRichTextSanitizer
{
    string Sanitize(string? html);
}
