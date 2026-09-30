namespace Elmanhg.Api.RateLimiting;

public static class AuthRateLimitPolicies
{
    public const string OtpRequests = "auth-otp-requests";
    public const string Credentials = "auth-credentials";
    public const string Refresh = "auth-refresh";
}
