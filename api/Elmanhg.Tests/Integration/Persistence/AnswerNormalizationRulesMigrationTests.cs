using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Infrastructure.Migrations;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AnswerNormalizationRulesMigrationTests(ApiFactory factory)
{
    [Fact]
    public async Task UpgradeSql_LegacyFillUnifyFalse_WritesLetterRulesOffOthersOn()
    {
        var questionId = await SeedLegacyAsync("Fill", """{"blanks":[{"id":"1","acceptedAnswers":["20"]}],"unifyLetterVariants":false}""");

        var (spec, snapshotSpec) = await UpgradeAndReadAsync(questionId);

        var expected = JsonNode.Parse("""{"blanks":[{"id":"1","acceptedAnswers":["20"]}],"normalization":{"stripTashkeel":true,"stripTatweel":true,"unifyAlef":false,"unifyTaaMarbuta":false,"unifyAlefMaqsura":false,"convertDigits":true,"collapseWhitespace":true,"foldCase":true}}""");
        JsonNode.DeepEquals(spec, expected).Should().BeTrue();
        JsonNode.DeepEquals(snapshotSpec, expected).Should().BeTrue();
    }

    [Fact]
    public async Task UpgradeSql_LegacyTextShortUnifyTrue_WritesAllRulesOn()
    {
        var questionId = await SeedLegacyAsync("Short", """{"acceptedAnswers":["ماء"],"unifyLetterVariants":true}""");

        var (spec, snapshotSpec) = await UpgradeAndReadAsync(questionId);

        var expected = JsonNode.Parse("""{"acceptedAnswers":["ماء"],"normalization":{"stripTashkeel":true,"stripTatweel":true,"unifyAlef":true,"unifyTaaMarbuta":true,"unifyAlefMaqsura":true,"convertDigits":true,"collapseWhitespace":true,"foldCase":true}}""");
        JsonNode.DeepEquals(spec, expected).Should().BeTrue();
        JsonNode.DeepEquals(snapshotSpec, expected).Should().BeTrue();
    }

    [Fact]
    public async Task UpgradeSql_NumericShortSpec_IsUnchanged()
    {
        const string numeric = """{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}""";
        var questionId = await SeedLegacyAsync("Short", numeric);

        var (spec, snapshotSpec) = await UpgradeAndReadAsync(questionId);

        JsonNode.DeepEquals(spec, JsonNode.Parse(numeric)).Should().BeTrue();
        JsonNode.DeepEquals(snapshotSpec, JsonNode.Parse(numeric)).Should().BeTrue();
    }

    private async Task<Guid> SeedLegacyAsync(string type, string legacy)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, cancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, ["State the first law"], cancellationToken).ConfigureAwait(false);
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken).ConfigureAwait(false);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
        await database.ExecuteSqlAsync($"UPDATE \"Questions\" SET \"Type\" = {type}, \"GradingSpec\" = {legacy}::jsonb WHERE \"Id\" = {questionId}", cancellationToken).ConfigureAwait(false);
        await database.ExecuteSqlAsync($"UPDATE \"QuestionRevisions\" SET \"Snapshot\" = jsonb_set(\"Snapshot\", ARRAY['gradingSpec'], {legacy}::jsonb) WHERE \"QuestionId\" = {questionId}", cancellationToken).ConfigureAwait(false);
        return questionId;
    }

    private async Task<(JsonNode? Spec, JsonNode? SnapshotSpec)> UpgradeAndReadAsync(Guid questionId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
            await database.ExecuteSqlRawAsync(AddAnswerNormalizationRules.UpgradeGradingSpecsSql, cancellationToken).ConfigureAwait(false);
            await database.ExecuteSqlRawAsync(AddAnswerNormalizationRules.UpgradeRevisionSnapshotsSql, cancellationToken).ConfigureAwait(false);
        }

        var question = await QuestionTestData.ReadQuestionAsync(factory, questionId, cancellationToken).ConfigureAwait(false);
        return (JsonNode.Parse(question.GradingSpec), JsonNode.Parse(question.Revisions.Single().Snapshot)!["gradingSpec"]);
    }
}
