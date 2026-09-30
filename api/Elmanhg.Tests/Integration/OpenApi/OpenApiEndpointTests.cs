using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.OpenApi;

public sealed class OpenApiEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Get_OpenApiDocument_Returns200Json()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        await using var body = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: TestContext.Current.CancellationToken);
        document.RootElement.TryGetProperty("openapi", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Get_ScalarReference_Returns200Html()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/scalar/v1", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task Get_OpenApiDocument_DescribesQuestionEnumsAsStrings()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        await using var body = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: TestContext.Current.CancellationToken);
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        schemas.GetProperty("QuestionType").GetProperty("enum").EnumerateArray().Select(x => x.GetString()).Should().Equal("Mcq", "Multi", "TrueFalse", "Fill", "Short", "Essay", "MathSteps", "DragDrop");
        schemas.GetProperty("QuestionValidationStatus").GetProperty("enum").EnumerateArray().Select(x => x.GetString()).Should().Contain("Rejected");
    }

    [Fact]
    public async Task Get_OpenApiDocument_DescribesImportTemplateAsBinary()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        await using var body = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: TestContext.Current.CancellationToken);
        var schema = document.RootElement.GetProperty("paths").GetProperty("/api/question-imports/template").GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet").GetProperty("schema");
        if (schema.TryGetProperty("$ref", out var reference))
        {
            schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1]);
        }

        schema.GetProperty("type").GetString().Should().Be("string");
        schema.GetProperty("format").GetString().Should().Be("binary");
    }
}
