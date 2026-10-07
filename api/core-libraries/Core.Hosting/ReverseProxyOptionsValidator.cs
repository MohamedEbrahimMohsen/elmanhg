using Microsoft.Extensions.Options;
using System.Net;

namespace Core.Hosting;

public sealed class ReverseProxyOptionsValidator : IValidateOptions<ReverseProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, ReverseProxyOptions options)
    {
        List<string> failures = [];
        for (var i = 0; i < options.TrustedNetworks.Count; i++)
        {
            if (!IPNetwork.TryParse(options.TrustedNetworks[i], out _))
            {
                failures.Add($"ReverseProxy:TrustedNetworks:{i} '{options.TrustedNetworks[i]}' is not a CIDR network such as 172.30.0.0/24.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
