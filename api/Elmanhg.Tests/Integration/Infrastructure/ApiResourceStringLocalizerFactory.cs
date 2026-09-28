using Microsoft.Extensions.Localization;

namespace Elmanhg.Tests.Integration.Infrastructure;

// Core.Localization.Localizer reads resources from the entry assembly, which under the test host is Elmanhg.Tests rather than Elmanhg.Api.
public sealed class ApiResourceStringLocalizerFactory(IStringLocalizerFactory inner) : IStringLocalizerFactory
{
    private static readonly string ApiAssemblyName = typeof(Program).Assembly.GetName().Name!;

    public IStringLocalizer Create(Type resourceSource)
    {
        return inner.Create(resourceSource);
    }

    public IStringLocalizer Create(string baseName, string location)
    {
        return inner.Create(baseName, ApiAssemblyName);
    }
}
