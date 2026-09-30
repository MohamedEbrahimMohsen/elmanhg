using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class DragDropQuestionRulesTests
{
    private const string MessyBody = $$"""{"image":{"key":"{{DragDropImageKey}}","width":800,"height":600,"alt":"  Plant cell "},"zones":[{"id":"z1","x":10,"y":10,"width":20,"height":15,"capacity":2,"extra":1},{"id":"z2","x":50,"y":40,"width":30,"height":20.5,"capacity":2}],"items":[{"id":"i1","text":" Nucleus "},{"id":"i2","text":"Vacuole"},{"id":"i3","text":"Wall"},{"id":"i4","text":"Membrane"},{"id":"i5","text":"Engine"}],"extra":1}""";
    private const string MessySpec = """{"zones":[{"zoneId":"z2","itemIds":["i4","i3"],"ordered":true},{"zoneId":"z1","itemIds":["i2","i1"]}]}""";
    private readonly ContentOptions _options = new();

    [Fact]
    public void Validate_ValidDragDrop_ReturnsNoErrors()
    {
        Errors(Json(DragDropBodyJson), Json(DragDropSpecJson)).Should().BeEmpty();
    }

    [Fact]
    public void Validate_BodyNotObject_ReturnsQuestionBodyInvalid()
    {
        Errors(Json("[]"), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionBodyInvalid);
    }

    [Fact]
    public void Validate_SpecNotObject_ReturnsQuestionGradingSpecInvalid()
    {
        Errors(Json(DragDropBodyJson), Json("\"x\"")).Should().Contain(ErrorCodes.QuestionGradingSpecInvalid);
    }

    [Fact]
    public void Validate_ImageMissing_ReturnsQuestionDiagramImageInvalid()
    {
        Errors(Body(x => x.AsObject().Remove("image")), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramImageInvalid);
    }

    [Theory]
    [InlineData("https://evil.example/x.png")]
    [InlineData("question-diagrams/0b5c1f4e-3d2a-4c8e-9f1a-2b3c4d5e6f70/0123456789abcdef0123456789abcdef.svg")]
    [InlineData("/api/media/question-diagrams/0b5c1f4e-3d2a-4c8e-9f1a-2b3c4d5e6f70/0123456789abcdef0123456789abcdef.png")]
    [InlineData("javascript:alert(1)")]
    public void Validate_ImageKeyNotAStoredDiagram_ReturnsQuestionDiagramImageInvalid(string key)
    {
        Errors(Body(x => x["image"]!["key"] = key), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramImageInvalid);
    }

    [Theory]
    [InlineData("width", 0)]
    [InlineData("height", 10001)]
    public void Validate_ImageSizeOutOfRange_ReturnsQuestionDiagramImageInvalid(string property, int value)
    {
        Errors(Body(x => x["image"]![property] = value), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramImageInvalid);
    }

    [Fact]
    public void Validate_BlankAlt_ReturnsQuestionDiagramImageAltRequired()
    {
        Errors(Body(x => x["image"]!["alt"] = "  "), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramImageAltRequired);
    }

    [Fact]
    public void Validate_AltTooLong_ReturnsQuestionDiagramImageAltTooLong()
    {
        var options = new ContentOptions { QuestionDiagramImageAltMaxLength = 3 };

        Errors(Body(x => x["image"]!["alt"] = "Cell"), Json(DragDropSpecJson), options).Should().Contain(ErrorCodes.QuestionDiagramImageAltTooLong);
    }

    [Fact]
    public void Validate_NoZones_ReturnsQuestionDiagramZonesCountInvalid()
    {
        Errors(Body(x => x["zones"] = new JsonArray()), Json("""{"zones":[]}""")).Should().Contain(ErrorCodes.QuestionDiagramZonesCountInvalid);
    }

    [Fact]
    public void Validate_TooManyZones_ReturnsQuestionDiagramZonesCountInvalid()
    {
        var options = new ContentOptions { QuestionDiagramZonesMaxCount = 1 };

        Errors(Json(DragDropBodyJson), Json(DragDropSpecJson), options).Should().Contain(ErrorCodes.QuestionDiagramZonesCountInvalid);
    }

    [Fact]
    public void Validate_ZoneIdInvalid_ReturnsQuestionDiagramZoneIdInvalid()
    {
        Errors(Zone(0, "id", "Z 1"), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramZoneIdInvalid);
    }

    [Fact]
    public void Validate_ZoneIdDuplicate_ReturnsQuestionDiagramZoneIdDuplicate()
    {
        Errors(Zone(1, "id", "z1"), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramZoneIdDuplicate);
    }

    [Theory]
    [InlineData("x", "-1", null)]
    [InlineData("x", "90", "20")]
    [InlineData("width", "1", null)]
    [InlineData("x", "10.555", null)]
    [InlineData("y", "null", null)]
    public void Validate_ZoneOutOfBounds_ReturnsQuestionDiagramZoneBoundsInvalid(string property, string value, string? width)
    {
        var body = Body(x =>
        {
            x["zones"]![0]![property] = JsonNode.Parse(value);
            if (width is not null)
            {
                x["zones"]![0]!["width"] = JsonNode.Parse(width);
            }
        });

        Errors(body, Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramZoneBoundsInvalid);
    }

    [Theory]
    [InlineData(0, 10, 50, 10)]
    [InlineData(50, 10, 0, 10)]
    [InlineData(10, 0, 10, 50)]
    [InlineData(10, 50, 10, 0)]
    public void Validate_ZonesTouching_ReturnsNoOverlap(double firstX, double firstY, double secondX, double secondY)
    {
        var body = Body(x =>
        {
            SetZone(x, 0, firstX, firstY, 50, 50);
            SetZone(x, 1, secondX, secondY, 50, 50);
        });

        Errors(body, Json(DragDropSpecJson)).Should().NotContain(ErrorCodes.QuestionDiagramZonesOverlap);
    }

    [Fact]
    public void Validate_ZonesOverlap_ReturnsQuestionDiagramZonesOverlap()
    {
        var body = Body(x =>
        {
            x["zones"]![1]!["x"] = 25;
            x["zones"]![1]!["y"] = 20;
        });

        Errors(body, Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramZonesOverlap);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Validate_ZoneCapacityOutOfRange_ReturnsQuestionDiagramZoneCapacityInvalid(int capacity)
    {
        Errors(Zone(0, "capacity", capacity), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramZoneCapacityInvalid);
    }

    [Fact]
    public void Validate_NoItems_ReturnsQuestionDiagramItemsCountInvalid()
    {
        Errors(Body(x => x["items"] = new JsonArray()), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramItemsCountInvalid);
    }

    [Fact]
    public void Validate_TooManyItems_ReturnsQuestionDiagramItemsCountInvalid()
    {
        var options = new ContentOptions { QuestionDiagramItemsMaxCount = 4 };

        Errors(Json(DragDropBodyJson), Json(DragDropSpecJson), options).Should().Contain(ErrorCodes.QuestionDiagramItemsCountInvalid);
    }

    [Fact]
    public void Validate_ItemIdInvalid_ReturnsQuestionDiagramItemIdInvalid()
    {
        Errors(Body(x => x["items"]![4]!["id"] = "I_1"), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramItemIdInvalid);
    }

    [Fact]
    public void Validate_ItemIdDuplicate_ReturnsQuestionDiagramItemIdDuplicate()
    {
        Errors(Body(x => x["items"]![4]!["id"] = "i1"), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramItemIdDuplicate);
    }

    [Fact]
    public void Validate_BlankItemText_ReturnsQuestionDiagramItemTextRequired()
    {
        Errors(Body(x => x["items"]![0]!["text"] = " "), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramItemTextRequired);
    }

    [Fact]
    public void Validate_ItemTextTooLong_ReturnsQuestionDiagramItemTextTooLong()
    {
        var options = new ContentOptions { QuestionDiagramItemTextMaxLength = 3 };

        Errors(Json(DragDropBodyJson), Json(DragDropSpecJson), options).Should().Contain(ErrorCodes.QuestionDiagramItemTextTooLong);
    }

    [Theory]
    [InlineData("""{"zones":[{"zoneId":"z1","itemIds":["i1"],"ordered":false}]}""")]
    [InlineData("""{"zones":[{"zoneId":"z1","itemIds":["i1"],"ordered":false},{"zoneId":"z1","itemIds":[],"ordered":false}]}""")]
    [InlineData("""{"zones":[{"zoneId":"z1","itemIds":["i1"],"ordered":false},{"zoneId":"z9","itemIds":[],"ordered":false}]}""")]
    public void Validate_KeyZonesDoNotMatch_ReturnsQuestionDiagramKeyZonesMismatch(string spec)
    {
        Errors(Json(DragDropBodyJson), Json(spec)).Should().Contain(ErrorCodes.QuestionDiagramKeyZonesMismatch);
    }

    [Theory]
    [InlineData("""{"zones":[{"zoneId":"z1","itemIds":["i9"],"ordered":false},{"zoneId":"z2","itemIds":[],"ordered":false}]}""")]
    [InlineData("""{"zones":[{"zoneId":"z1","itemIds":["i1"],"ordered":false},{"zoneId":"z2","itemIds":["i1"],"ordered":false}]}""")]
    [InlineData("""{"zones":[{"zoneId":"z1","itemIds":["i1","i1"],"ordered":false},{"zoneId":"z2","itemIds":[],"ordered":false}]}""")]
    public void Validate_KeyItemUnknownOrRepeated_ReturnsQuestionDiagramKeyItemInvalid(string spec)
    {
        Errors(Json(DragDropBodyJson), Json(spec)).Should().Contain(ErrorCodes.QuestionDiagramKeyItemInvalid);
    }

    [Fact]
    public void Validate_ZoneOverCapacity_ReturnsQuestionDiagramZoneOverCapacity()
    {
        Errors(Zone(0, "capacity", 1), Json(DragDropSpecJson)).Should().Contain(ErrorCodes.QuestionDiagramZoneOverCapacity);
    }

    [Fact]
    public void Validate_NoItemPlaced_ReturnsQuestionDiagramKeyEmpty()
    {
        var spec = """{"zones":[{"zoneId":"z1","itemIds":[],"ordered":false},{"zoneId":"z2","itemIds":[],"ordered":false}]}""";

        Errors(Json(DragDropBodyJson), Json(spec)).Should().Contain(ErrorCodes.QuestionDiagramKeyEmpty);
    }

    [Fact]
    public void Validate_OrderedZoneWithOneItem_ReturnsQuestionDiagramOrderInvalid()
    {
        var spec = """{"zones":[{"zoneId":"z1","itemIds":["i1","i2"],"ordered":false},{"zoneId":"z2","itemIds":["i4"],"ordered":true}]}""";

        Errors(Json(DragDropBodyJson), Json(spec)).Should().Contain(ErrorCodes.QuestionDiagramOrderInvalid);
    }

    [Fact]
    public void Validate_DistractorAndEmptyZone_ReturnsNoErrors()
    {
        var spec = """{"zones":[{"zoneId":"z1","itemIds":["i1","i2"],"ordered":false},{"zoneId":"z2","itemIds":[],"ordered":false}]}""";

        Errors(Json(DragDropBodyJson), Json(spec)).Should().BeEmpty();
    }

    [Fact]
    public void Normalize_DragDrop_TrimsDropsUnknownPropsAndCanonicalisesKey()
    {
        var (body, spec) = DragDropQuestionRules.Normalize(Json(MessyBody), Json(MessySpec));

        JsonNode.DeepEquals(JsonNode.Parse(body), JsonNode.Parse(DragDropBodyJson)).Should().BeTrue(body);
        JsonNode.DeepEquals(JsonNode.Parse(spec), JsonNode.Parse(DragDropSpecJson)).Should().BeTrue(spec);
    }

    [Fact]
    public void Normalize_DragDropImageUrlSentInBody_IsDropped()
    {
        var sent = Body(x => x["image"]!["url"] = "https://evil.example/cell.png");

        var (body, _) = DragDropQuestionRules.Normalize(sent, Json(DragDropSpecJson));

        JsonNode.Parse(body)!["image"]!.AsObject().ContainsKey("url").Should().BeFalse(body);
        JsonNode.DeepEquals(JsonNode.Parse(body), JsonNode.Parse(DragDropBodyJson)).Should().BeTrue(body);
    }

    private static void SetZone(JsonNode body, int index, double x, double y, double width, double height)
    {
        var zone = body["zones"]![index]!;
        zone["x"] = x;
        zone["y"] = y;
        zone["width"] = width;
        zone["height"] = height;
    }

    private static JsonElement Zone(int index, string property, JsonNode value) => Body(x => x["zones"]![index]![property] = value);

    private List<string> Errors(JsonElement body, JsonElement spec, ContentOptions? options = null) => DragDropQuestionRules.Validate(body, spec, options ?? _options);

    private static JsonElement Body(Action<JsonNode> change) => Change(DragDropBodyJson, change);

    private static JsonElement Change(string json, Action<JsonNode> change)
    {
        var node = JsonNode.Parse(json)!;
        change(node);
        return Json(node.ToJsonString());
    }
}
