using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Observability.ReportClientError;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Tests.Application.Features.Shared.Observability;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Diagnostics.Metrics;

namespace Elmanhg.Tests.Application.Features.Observability.ReportClientError;

public sealed class ReportClientErrorHandlerTests : IDisposable
{
    private const string RawEmail = "mona@example.com";
    private const string RawPhone = "01012345678";
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ILogger<ReportClientErrorHandler> _logger = Substitute.For<ILogger<ReportClientErrorHandler>>();
    private readonly MetricCollector<long> _errors;
    private readonly ReportClientErrorHandler _handler;

    public ReportClientErrorHandlerTests()
    {
        _errors = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.client.errors");
        _currentUserService.UserId.Returns((Guid?)null);
        _handler = new ReportClientErrorHandler(new ElmanhgMetrics(_meterFactory), _currentUserService, _logger);
    }

    [Fact]
    public async Task Handle_Report_RecordsClientErrorMetric()
    {
        await _handler.Handle(new ReportClientErrorCommand("boom", "Error", null, ClientErrorSource.Window, "/student"), TestContext.Current.CancellationToken);

        _errors.GetMeasurementSnapshot().Should().ContainSingle().Which.Tags.Should().Contain(ElmanhgMetrics.SourceTag, "Window");
    }

    [Fact]
    public async Task Handle_ReportWithEmailAndPhone_LogsRedactedWarning()
    {
        var command = new ReportClientErrorCommand($"lookup {RawEmail} failed", "Error", $"at call({RawPhone})", ClientErrorSource.Route, "/student");

        await _handler.Handle(command, TestContext.Current.CancellationToken);

        var (level, message) = LoggedWarning();
        level.Should().Be(LogLevel.Warning);
        message.Should().Contain(LogRedactor.EmailReplacement).And.Contain(LogRedactor.PhoneReplacement);
        message.Should().NotContain(RawEmail).And.NotContain(RawPhone);
    }

    [Fact]
    public async Task Handle_SignedInUser_LogsUserId()
    {
        var userId = Guid.CreateVersion7();
        _currentUserService.UserId.Returns(userId);

        await _handler.Handle(new ReportClientErrorCommand("boom", null, null, ClientErrorSource.UnhandledRejection, null), TestContext.Current.CancellationToken);

        LoggedWarning().Message.Should().Contain(userId.ToString());
    }

    public void Dispose() => _errors.Dispose();

    private (LogLevel Level, string Message) LoggedWarning()
    {
        var call = _logger.ReceivedCalls().Should().ContainSingle(x => x.GetMethodInfo().Name == nameof(ILogger.Log)).Subject;
        var arguments = call.GetArguments();
        return ((LogLevel)arguments[0]!, arguments[2]!.ToString()!);
    }
}
