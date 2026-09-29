using Core.Logging;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Net;

namespace Elmanhg.Tests.Core.Logging;

[CollectionDefinition(nameof(GlobalSerilogLoggerCollection), DisableParallelization = true)]
public sealed class GlobalSerilogLoggerCollection;

[Collection(nameof(GlobalSerilogLoggerCollection))]
public sealed class CoreRequestLoggingMiddlewareTests
{
    [Fact]
    public async Task Invoke_PaymobWebhookWithHmac_LogsQueryKeysWithoutValues()
    {
        var properties = await InvokeAndCaptureAsync("/api/payments/paymob/webhook", "?hmac=4f1c9a0b7e2d");

        properties["QueryString"].Should().Be("?hmac=[redacted]");
    }

    [Fact]
    public async Task Invoke_AuditLogsFilteredByEncodedEmail_LogsQueryKeysWithoutValues()
    {
        var properties = await InvokeAndCaptureAsync("/api/audit-logs", "?actor=mona%40example.com");

        properties["QueryString"].Should().Be("?actor=[redacted]");
        properties.Values.Should().NotContain(value => value != null && value.Contains("mona", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invoke_ClientAddress_LogsTruncatedIpWithoutCoordinates()
    {
        var properties = await InvokeAndCaptureAsync("/api/sessions", string.Empty);

        properties["ClientIp"].Should().Be("203.0.113.0/24");
        properties["ForwardedFor"].Should().Be("198.51.100.0/24");
        properties.Should().NotContainKeys("Latitude", "Longitude");
    }

    private static async Task<IReadOnlyDictionary<string, string?>> InvokeAndCaptureAsync(string path, string query)
    {
        var sink = new CapturingSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        try
        {
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(Environments.Production);
            var middleware = new CoreRequestLoggingMiddleware(_ => Task.CompletedTask, environment, new ConfigurationBuilder().Build());
            var context = new DefaultHttpContext { TraceIdentifier = Guid.NewGuid().ToString("N") };
            context.Request.Path = path;
            context.Request.QueryString = new QueryString(query);
            context.Request.Headers["CF-IPLatitude"] = "30.04";
            context.Request.Headers["CF-IPLongitude"] = "31.23";
            context.Request.Headers["X-Forwarded-For"] = "198.51.100.9";
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.77");

            await middleware.Invoke(context);

            return sink.Events.Single(logEvent => logEvent.Properties.ContainsKey("IsRequestLog"))
                .Properties.ToDictionary(pair => pair.Key, pair => pair.Value is ScalarValue { Value: var value } ? value?.ToString() : pair.Value.ToString());
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
