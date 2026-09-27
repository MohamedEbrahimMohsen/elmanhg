using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Range(1, int.MaxValue)]
    public int DisplayNameMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int EmailMaxLength { get; set; }

    [Required]
    public string RefreshTokenCookieName { get; set; } = default!;

    [Required]
    public string RefreshTokenCookiePath { get; set; } = default!;

    public bool RefreshTokenCookieSecure { get; set; }

    [Range(1, int.MaxValue)]
    public int OtpRequestPermitLimit { get; set; }

    [Range(1, int.MaxValue)]
    public int OtpRequestWindowSeconds { get; set; }

    [Range(1, int.MaxValue)]
    public int CredentialPermitLimit { get; set; }

    [Range(1, int.MaxValue)]
    public int CredentialWindowSeconds { get; set; }
}
