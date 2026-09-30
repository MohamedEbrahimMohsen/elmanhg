using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public static class DragDropQuestionRules
{
    public static List<string> Validate(JsonElement body, JsonElement gradingSpec, ContentOptions options)
    {
        List<string> errors = [];
        var bodyRead = QuestionSchemaReader.TryRead<DragDropBody>(body, out var diagram);
        var specRead = QuestionSchemaReader.TryRead<DragDropGradingSpec>(gradingSpec, out var spec);
        QuestionSchemaReader.AddIf(errors, !bodyRead, ErrorCodes.QuestionBodyInvalid);
        QuestionSchemaReader.AddIf(errors, !specRead, ErrorCodes.QuestionGradingSpecInvalid);
        if (!bodyRead || !specRead)
        {
            return errors;
        }

        AddImageErrors(errors, diagram!.Image, options);
        DiagramZoneRules.AddErrors(errors, diagram.Zones, options);
        AddItemErrors(errors, diagram.Items, options);
        DiagramKeyRules.AddErrors(errors, diagram.Zones ?? [], diagram.Items ?? [], spec!.Zones);
        return errors;
    }

    public static (string Body, string GradingSpec) Normalize(JsonElement body, JsonElement gradingSpec)
    {
        var diagram = QuestionSchemaReader.Read<DragDropBody>(body);
        var spec = QuestionSchemaReader.Read<DragDropGradingSpec>(gradingSpec);
        var source = diagram.Image!;
        var image = new DiagramImage(source.Key, source.Width, source.Height, source.Alt!.Trim());
        var zones = diagram.Zones!
            .Select(x => new DiagramZone(x.Id, x.X, x.Y, x.Width, x.Height, x.Capacity))
            .ToList();
        var items = diagram.Items!
            .Select(x => new DiagramItem(x.Id, x.Text!.Trim()))
            .ToList();
        var itemOrder = items
            .Select((item, index) => (Id: item.Id!, Index: index))
            .ToDictionary(x => x.Id, x => x.Index, StringComparer.Ordinal);
        var keys = zones
            .Select(zone => Key(zone, spec.Zones!, itemOrder))
            .ToList();
        return (QuestionSchemaReader.Serialize(new DragDropBody(image, zones, items)), QuestionSchemaReader.Serialize(new DragDropGradingSpec(keys)));
    }

    private static DiagramZoneKey Key(DiagramZone zone, List<DiagramZoneKey> keys, Dictionary<string, int> itemOrder)
    {
        var key = keys.First(x => string.Equals(x.ZoneId, zone.Id, StringComparison.Ordinal));
        var ordered = key.Ordered ?? false;
        var ids = ordered
            ? key.ItemIds ?? []
            : (key.ItemIds ?? [])
                .OrderBy(id => itemOrder[id])
                .ToList();
        return new DiagramZoneKey(zone.Id, ids, ordered);
    }

    private static void AddImageErrors(List<string> errors, DiagramImage? image, ContentOptions options)
    {
        QuestionSchemaReader.AddIf(errors, image is null || !DiagramImageKey.IsValid(image.Key) || !IsDimension(image.Width, options) || !IsDimension(image.Height, options), ErrorCodes.QuestionDiagramImageInvalid);
        QuestionSchemaReader.AddIf(errors, string.IsNullOrWhiteSpace(image?.Alt), ErrorCodes.QuestionDiagramImageAltRequired);
        QuestionSchemaReader.AddIf(errors, image?.Alt is { } alt && alt.Trim().Length > options.QuestionDiagramImageAltMaxLength, ErrorCodes.QuestionDiagramImageAltTooLong);
    }

    private static bool IsDimension(int? value, ContentOptions options) => value >= 1 && value <= options.QuestionDiagramImageDimensionMax;

    private static void AddItemErrors(List<string> errors, List<DiagramItem>? items, ContentOptions options)
    {
        var list = items ?? [];
        var ids = list
            .Where(x => x?.Id is not null)
            .Select(x => x!.Id!)
            .ToList();
        QuestionSchemaReader.AddIf(errors, list.Count < 1 || list.Count > options.QuestionDiagramItemsMaxCount, ErrorCodes.QuestionDiagramItemsCountInvalid);
        QuestionSchemaReader.AddIf(errors, list.Any(x => x is null || !QuestionSchemaReader.IsValidId(x.Id)), ErrorCodes.QuestionDiagramItemIdInvalid);
        QuestionSchemaReader.AddIf(errors, ids.Distinct(StringComparer.Ordinal).Count() != ids.Count, ErrorCodes.QuestionDiagramItemIdDuplicate);
        QuestionSchemaReader.AddIf(errors, list.Any(x => string.IsNullOrWhiteSpace(x?.Text)), ErrorCodes.QuestionDiagramItemTextRequired);
        QuestionSchemaReader.AddIf(errors, list.Any(x => x?.Text is { } text && text.Trim().Length > options.QuestionDiagramItemTextMaxLength), ErrorCodes.QuestionDiagramItemTextTooLong);
    }
}
