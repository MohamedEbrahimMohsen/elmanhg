using Core.OTP.Delivery;
using System.Globalization;
using System.Net;

namespace Elmanhg.Infrastructure.OtpDelivery.Email;

public static class OtpEmailTemplate
{
    private const string CodePlaceholder = "{{code}}";
    private const string MinutesPlaceholder = "{{minutes}}";
    private const string ResourcePrefix = "Elmanhg.Infrastructure.OtpDelivery.Email.Templates.";

    private static readonly Lazy<string> Html = new(() => Read("OtpEmail.html"));
    private static readonly Lazy<string> Text = new(() => Read("OtpEmail.txt"));

    public static OtpEmailContent Render(string code, int expirationMinutes) => new(RenderHtml(code, expirationMinutes), RenderText(code, expirationMinutes));

    public static string RenderHtml(string code, int expirationMinutes) => Html.Value
        .Replace(CodePlaceholder, WebUtility.HtmlEncode(code), StringComparison.Ordinal)
        .Replace(MinutesPlaceholder, expirationMinutes.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    public static string RenderText(string code, int expirationMinutes) => Text.Value
        .Replace(CodePlaceholder, code, StringComparison.Ordinal)
        .Replace(MinutesPlaceholder, expirationMinutes.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static string Read(string fileName)
    {
        using var stream = typeof(OtpEmailTemplate).Assembly.GetManifestResourceStream(ResourcePrefix + fileName) ?? throw new InvalidOperationException($"Missing embedded template {fileName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
