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
    public async Task Post_MultiPartialInEnglish_ReturnsTallyFeedback()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await AdminClientAsync();
        admin.DefaultRequestHeaders.Add("Accept-Language", "en");

        using var response = await admin.PostAsJsonAsync(Route, Draft("Multi", "<p>Vectors?</p>", """{"options":[{"id":"a","text":"Force"},{"id":"b","text":"Velocity"},{"id":"c","text":"Mass"}]}""", """{"correctOptionIds":["a","b"],"partialCredit":true}""", """{"optionIds":["a"]}"""), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("feedback").GetString().Should().Be("Correct choices: 1 of 2; wrong choices: 0.");
    }

    [Fact]
    public async Task Post_McqUnansweredInArabic_ReturnsUnansweredFeedback()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await AdminClientAsync();
        admin.DefaultRequestHeaders.Add("Accept-Language", "ar");

        using var response = await admin.PostAsJsonAsync(Route, Draft("Mcq", "<p>2 + 2 = ?</p>", """{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}""", """{"correctOptionId":"b"}""", "{}"), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("outcome").GetString().Should().Be("Incorrect");
        body.GetProperty("feedback").GetString().Should().Be("لم تتم الإجابة عن السؤال.");
    }

    [Fact]
    public async Task Post_FillPartialInEnglish_ReturnsBlankTallyFeedback()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await AdminClientAsync();
        admin.DefaultRequestHeaders.Add("Accept-Language", "en");

        using var response = await admin.PostAsJsonAsync(Route, Draft("Fill", "<p>[[1]] + [[2]]</p>", """{"blanks":[{"id":"1"},{"id":"2"}]}""", """{"blanks":[{"id":"1","acceptedAnswers":["20"]},{"id":"2","acceptedAnswers":["5"]}]}""", """{"blanks":[{"id":"1","text":"20"},{"id":"2","text":"7"}]}"""), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("outcome").GetString().Should().Be("Partial");
        body.GetProperty("feedback").GetString().Should().Be("Correct blanks: 1 of 2.");
    }

    [Fact]
    public async Task Post_ShortNumericWithUnitInArabic_ReturnsNotANumberFeedback()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await AdminClientAsync();
        admin.DefaultRequestHeaders.Add("Accept-Language", "ar");

        using var response = await admin.PostAsJsonAsync(Route, Draft("Short", "<p>g = ?</p>", """{"answerKind":"numeric"}""", """{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}""", """{"text":"9.8 m/s"}"""), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("outcome").GetString().Should().Be("Incorrect");
        body.GetProperty("feedback").GetString().Should().Be("اكتب الإجابة رقمًا فقط، بدون وحدات.");
    }

    [Fact]
    public async Task Post_ShortNumericSpecAtDecimalLimit_ReturnsCorrect()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, Draft("Short", "<p>g = ?</p>", """{"answerKind":"numeric"}""", """{"value":79228162514264337593543950335,"tolerance":1,"toleranceMode":"absolute"}""", """{"text":"79228162514264337593543950335"}"""), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("outcome").GetString().Should().Be("Correct");
    }

    [Fact]
    public async Task Post_CorrectTrueFalse_ReturnsNullFeedback()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, Draft("TrueFalse", "<p>Mass is a vector.</p>", "{}", """{"correctAnswer":false}""", """{"value":false}"""), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("feedback").ValueKind.Should().Be(JsonValueKind.Null);
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
    public async Task Post_FillWithTaaMarbutaRuleOff_GradesVariantIncorrect()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, Draft("Fill", "<p>[[1]]</p>", """{"blanks":[{"id":"1"}]}""", """{"blanks":[{"id":"1","acceptedAnswers":["القاهرة"]}],"normalization":{"unifyTaaMarbuta":false}}""", """{"blanks":[{"id":"1","text":"القاهره"}]}"""), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("outcome").GetString().Should().Be("Incorrect");
    }

    [Fact]
    public async Task Post_NormalizationNotAnObject_Returns422QuestionGradingSpecInvalid()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, Draft("Fill", "<p>[[1]]</p>", """{"blanks":[{"id":"1"}]}""", """{"blanks":[{"id":"1","acceptedAnswers":["20"]}],"normalization":"off"}""", """{"blanks":[{"id":"1","text":"20"}]}"""), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("QUESTION_GRADING_SPEC_INVALID");
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
