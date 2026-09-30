using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;

namespace Elmanhg.Tests.Integration.Composition;

public sealed class SecurityPostureTests(ApiFactory factory)
{
    [Fact]
    public async Task Get_CrossOriginRequest_ReturnsNoCorsHeaders()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/plans");
        request.Headers.Add("Origin", "https://evil.example");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
