using System.Globalization;

namespace Elmanhg.Tests.Core.Localization;

public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo? _previousDefaultThreadCulture = CultureInfo.DefaultThreadCurrentCulture;

    public CultureScope(string culture)
    {
        CultureInfo.CurrentCulture = new CultureInfo(culture);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.DefaultThreadCurrentCulture = _previousDefaultThreadCulture;
    }
}
