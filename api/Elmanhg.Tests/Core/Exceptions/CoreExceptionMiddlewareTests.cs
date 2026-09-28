using Core.Errors;
using Core.Exceptions;
using Core.Localization;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text.Json;

namespace Elmanhg.Tests.Core.Exceptions;

public sealed class CoreExceptionMiddlewareTests
{
    private readonly DefaultHttpContext _context = CreateContext();

    [Fact]
    public async Task InvokeAsync_CoreExceptionThrown_WritesStatusAndCamelCaseCode()
    {
        var middleware = new CoreExceptionMiddleware(_ => throw new NotFoundCoreException("PROBE_NOT_FOUND"), NullLogger<CoreExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(_context);

        _context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        using var body = await ReadBodyAsync(_context);
        body.RootElement.GetProperty("code").GetString().Should().Be("PROBE_NOT_FOUND");
    }

    [Fact]
    public async Task InvokeAsync_ServiceUnavailableThrown_Returns503WithCode()
    {
        var middleware = new CoreExceptionMiddleware(_ => throw new ServiceUnavailableCoreException("PROBE_UNAVAILABLE"), NullLogger<CoreExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(_context);

        _context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        using var body = await ReadBodyAsync(_context);
        body.RootElement.GetProperty("code").GetString().Should().Be("PROBE_UNAVAILABLE");
    }

    [Fact]
    public async Task InvokeAsync_UnhandledExceptionThrown_Returns500WithUnhandledCode()
    {
        var middleware = new CoreExceptionMiddleware(_ => throw new InvalidOperationException("boom"), NullLogger<CoreExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(_context);

        _context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        using var body = await ReadBodyAsync(_context);
        body.RootElement.GetProperty("code").GetString().Should().Be(ExceptionErrorCodes.UnhandledException);
        body.RootElement.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task InvokeAsync_NoException_LeavesResponseUntouched()
    {
        var middleware = new CoreExceptionMiddleware(_ => Task.CompletedTask, NullLogger<CoreExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(_context);

        _context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _context.Response.Body.Length.Should().Be(0);
    }

    private static DefaultHttpContext CreateContext()
    {
        var localizer = Substitute.For<ILocalizer>();
        localizer.GetMessage(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns(call => call.ArgAt<string?>(1) ?? string.Empty);
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(Environments.Production);
        return new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
            RequestServices = new ServiceCollection().AddSingleton<IErrorResponseHandler>(new ErrorResponseHandler(localizer, hostEnvironment)).BuildServiceProvider(),
        };
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);
    }
}
