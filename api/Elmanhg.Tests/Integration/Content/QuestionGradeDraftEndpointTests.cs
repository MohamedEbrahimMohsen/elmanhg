using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Integration.Content;

public sealed class QuestionGradeDraftEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/questions/grade-draft";

    [Fact]
    public async Task Post_CorrectMcqAnswer_ReturnsCorrect()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, Draft("Mcq", "<p>2 + 2 = ?</p>", """{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}""", """{"correctOptionId":"b"}""", """{"optionId":"b"}"""), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("outcome").GetString().Should().Be("Correct");
        body.GetProperty("score").GetDecimal().Should().Be(1m);
        body.GetProperty("maxScore").GetInt32().Should().Be(1);
    }

    [Theory]
    [InlineData("Multi", "<p>Vectors?</p>", """{"options":[{"id":"a","text":"Force"},{"id":"b","text":"Velocity"},{"id":"c","text":"Mass"}]}""", """{"correctOptionIds":["a","b"],"partialCredit":true}""", """{"optionIds":["a"]}""", "Partial")]
    [InlineData("TrueFalse", "<p>Mass is a vector.</p>", "{}", """{"correctAnswer":false}""", """{"value":false}""", "Correct")]
    [InlineData("Fill", "<p>v = [[1]] m/s</p>", """{"blanks":[{"id":"1"}]}""", """{"blanks":[{"id":"1","acceptedAnswers":["20"]}]}""", """{"blanks":[{"id":"1","text":"٢٠"}]}""", "Correct")]
    [InlineData("Short", "<p>g = ?</p>", """{"answerKind":"numeric"}""", """{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}""", """{"text":"9.75"}""", "Correct")]
    [InlineData("Mcq", "<p>2 + 2 = ?</p>", """{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}""", """{"correctOptionId":"b"}""", """{"optionId":"a"}""", "Incorrect")]
    public async Task Post_EachV1Type_GradesAnswer(string type, string stem, string body, string gradingSpec, string answer, string outcome)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, Draft(type, stem, body, gradingSpec, answer), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("outcome").GetString().Should().Be(outcome);
    }

    [Fact]
    public async Task Post_InvalidDraft_Returns422QuestionCorrectOptionInvalid()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, Draft("Mcq", "<p>2 + 2 = ?</p>", """{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}""", """{"correctOptionId":"z"}""", """{"optionId":"b"}"""), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("QUESTION_CORRECT_OPTION_INVALID");
    }

    [Fact]
    public async Task Post_AnswerWrongShape_Returns422QuestionAnswerInvalid()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, Draft("Mcq", "<p>2 + 2 = ?</p>", """{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}""", """{"correctOptionId":"b"}""", """{"optionId":5}"""), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("QUESTION_ANSWER_INVALID");
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, cancellationToken);

        using var response = await client.PostAsJsonAsync(Route, Draft("TrueFalse", "<p>Mass is a vector.</p>", "{}", """{"correctAnswer":false}""", """{"value":false}"""), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsJsonAsync(Route, Draft("TrueFalse", "<p>Mass is a vector.</p>", "{}", """{"correctAnswer":false}""", """{"value":false}"""), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static object Draft(string type, string stem, string body, string gradingSpec, string answer)
    {
        return new { type, stem, body = Json(body), gradingSpec = Json(gradingSpec), difficulty = "Medium", maxScore = 1, answer = Json(answer) };
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
