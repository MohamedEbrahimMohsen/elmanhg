using Core.Http;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Http;

public static class CoreHttpTestSettings
{
    public const string UserAgent = "Elmanhg/1.0";

    public static IOptions<CoreHttpOptions> Create() => Options.Create(new CoreHttpOptions { UserAgent = UserAgent });
}
