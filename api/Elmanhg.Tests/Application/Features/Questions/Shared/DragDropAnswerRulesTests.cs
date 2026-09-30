using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;
using System.Text.Json;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class DragDropAnswerRulesTests
{
    private readonly SessionsOptions _options = new();

    [Fact]
    public void CanRead_Placements_ReturnsTrue()
    {
        DragDropAnswerRules.CanRead(Json("""{"placements":[{"zoneId":"z1","itemIds":["i1","i2"]},{"zoneId":"z2"}]}""")).Should().BeTrue();
    }

    [Fact]
    public void CanRead_MissingPlacements_ReturnsTrue()
    {
        DragDropAnswerRules.CanRead(Json("{}")).Should().BeTrue();
    }

    [Theory]
    [InlineData("""{"placements":[null]}""")]
    [InlineData("""{"placements":[{"itemIds":["i1"]}]}""")]
    [InlineData("""{"placements":[{"zoneId":1,"itemIds":[]}]}""")]
    [InlineData("""{"placements":[{"zoneId":"z1","itemIds":[null]}]}""")]
    [InlineData("""{"placements":"x"}""")]
    public void CanRead_TypeViolation_ReturnsFalse(string answer)
    {
        DragDropAnswerRules.CanRead(Json(answer)).Should().BeFalse();
    }

    [Fact]
    public void Canonicalize_DropsEmptyPlacementsAndUnknownProperties()
    {
        var canonical = DragDropAnswerRules.Canonicalize(Json("""{"placements":[{"zoneId":"z1","itemIds":[],"x":1},{"zoneId":"z2","itemIds":["i4","i3"]}],"y":2}"""));

        QuestionJson.AreEquivalent(canonical, """{"placements":[{"zoneId":"z2","itemIds":["i4","i3"]}]}""").Should().BeTrue(canonical);
    }

    [Fact]
    public void ExceedsLimits_TooManyPlacements_ReturnsTrue()
    {
        DragDropAnswerRules.ExceedsLimits(Answer(Enumerable.Range(0, 21).Select(x => new[] { $"i{x}" })), _options).Should().BeTrue();
    }

    [Fact]
    public void ExceedsLimits_TooManyPlacedItems_ReturnsTrue()
    {
        DragDropAnswerRules.ExceedsLimits(Answer([Ids(0, 16), Ids(16, 15)]), _options).Should().BeTrue();
    }

    [Fact]
    public void ExceedsLimits_AtCaps_ReturnsFalse()
    {
        var placements = Enumerable.Range(0, 20).Select(x => x < 10 ? Ids(x * 2, 2) : Ids(20 + x, 1));

        DragDropAnswerRules.ExceedsLimits(Answer(placements), _options).Should().BeFalse();
    }

    private static string[] Ids(int start, int count) => Enumerable.Range(start, count)
        .Select(x => $"i{x}")
        .ToArray();

    private static JsonElement Answer(IEnumerable<string[]> placements) => Json(JsonSerializer.Serialize(new { placements = placements.Select((itemIds, index) => new { zoneId = $"z{index}", itemIds }) }));
}
