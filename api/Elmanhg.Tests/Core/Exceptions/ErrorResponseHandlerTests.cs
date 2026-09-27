using Core.Exceptions;
using Core.Localization;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Elmanhg.Tests.Core.Exceptions;

public sealed class ErrorResponseHandlerTests
{
    private readonly ILocalizer _localizer = CreateLocalizer();
    private readonly IHostEnvironment _hostEnvironment = Substitute.For<IHostEnvironment>();
    private readonly ExceptionDetails _exceptionDetails = new() { Code = "PROBE_FAILED", Message = "probe", StatusCode = 500, Exception = new InvalidOperationException("probe-detail") };

    [Fact]
    public void GenerateErrorResponse_DevelopmentEnvironment_IncludesExceptionDetail()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);
        var handler = new ErrorResponseHandler(_localizer, _hostEnvironment);

        var response = handler.GenerateErrorResponse(_exceptionDetails);

        response.Data.Should().Contain("probe-detail");
        response.Code.Should().Be("PROBE_FAILED");
    }

    [Fact]
    public void GenerateErrorResponse_ProductionEnvironment_OmitsExceptionDetail()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Production);
        var handler = new ErrorResponseHandler(_localizer, _hostEnvironment);

        var response = handler.GenerateErrorResponse(_exceptionDetails);

        response.Data.Should().BeNull();
    }

    private static ILocalizer CreateLocalizer()
    {
        var localizer = Substitute.For<ILocalizer>();
        localizer.GetMessage(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns(call => call.ArgAt<string?>(1) ?? string.Empty);
        return localizer;
    }
}
