using Elmanhg.Infrastructure.Hosting;
using FluentAssertions;

namespace Elmanhg.Tests.Infrastructure.Hosting;

public sealed class ReverseProxyOptionsValidatorTests
{
    private readonly ReverseProxyOptionsValidator _validator = new();

    [Fact]
    public void Validate_NoTrustedNetworks_Succeeds()
    {
        var result = _validator.Validate(null, new ReverseProxyOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_Ipv4AndIpv6Networks_Succeeds()
    {
        var options = new ReverseProxyOptions { TrustedNetworks = ["172.30.0.0/24", "fd00::/8"] };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-a-network")]
    [InlineData("10.0.0.0/33")]
    [InlineData("")]
    public void Validate_MalformedNetwork_FailsNamingEntry(string network)
    {
        var options = new ReverseProxyOptions { TrustedNetworks = ["172.30.0.0/24", network] };

        var result = _validator.Validate(null, options);

        result.Failures.Should().Contain(x => x.StartsWith("ReverseProxy:TrustedNetworks:1 '", StringComparison.Ordinal));
    }
}
