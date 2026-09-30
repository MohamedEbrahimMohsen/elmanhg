using Elmanhg.Domain.Identity;
using System.Net;

namespace Elmanhg.Infrastructure.Invitations;

public static class InvitationEmailTemplate
{
    private const string NamePlaceholder = "{{name}}";
    private const string RolePlaceholder = "{{role}}";
    private const string LinkPlaceholder = "{{link}}";
    private const string ResourcePrefix = "Elmanhg.Infrastructure.Invitations.Templates.";

    private static readonly Lazy<string> Html = new(() => Read("InvitationEmail.html"));
    private static readonly Lazy<string> Text = new(() => Read("InvitationEmail.txt"));

    public static string RenderHtml(string displayName, UserRole role, string link) => Html.Value
        .Replace(NamePlaceholder, WebUtility.HtmlEncode(displayName), StringComparison.Ordinal)
        .Replace(RolePlaceholder, RoleName(role), StringComparison.Ordinal)
        .Replace(LinkPlaceholder, WebUtility.HtmlEncode(link), StringComparison.Ordinal);

    public static string RenderText(string displayName, UserRole role, string link) => Text.Value
        .Replace(NamePlaceholder, displayName, StringComparison.Ordinal)
        .Replace(RolePlaceholder, RoleName(role), StringComparison.Ordinal)
        .Replace(LinkPlaceholder, link, StringComparison.Ordinal);

    private static string RoleName(UserRole role) => role switch
    {
        UserRole.Admin => "مديرًا",
        _ => "معلّمًا",
    };

    private static string Read(string fileName)
    {
        using var stream = typeof(InvitationEmailTemplate).Assembly.GetManifestResourceStream(ResourcePrefix + fileName) ?? throw new InvalidOperationException($"Missing embedded template {fileName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
