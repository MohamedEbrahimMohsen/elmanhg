namespace Elmanhg.Application.Dashboard.Shared;

public static class DashboardRates
{
    // Presentation precision; not a tunable.
    public const int RateDecimals = 4;
    // Presentation precision; not a tunable.
    public const int PerStudentDecimals = 2;

    public static decimal? Ratio(long numerator, long denominator, int decimals) => denominator == 0 ? null : Math.Round((decimal)numerator / denominator, decimals, MidpointRounding.AwayFromZero);

    public static long? Seconds(double? seconds) => seconds is null ? null : (long)Math.Round(seconds.Value, MidpointRounding.AwayFromZero);
}
