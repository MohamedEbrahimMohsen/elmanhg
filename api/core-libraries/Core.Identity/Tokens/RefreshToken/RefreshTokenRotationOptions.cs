namespace Core.Identity.Tokens.RefreshToken;

public sealed class RefreshTokenRotationOptions
{
    public TimeSpan ReuseGrace { get; set; } = TimeSpan.FromSeconds(10);
}
