using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Questions.Schemas;

namespace Elmanhg.Application.Questions.Shared;

public static class DiagramKeyRules
{
    // An order constraint needs at least two items to order.
    private const int MinimumOrderedItems = 2;

    public static void AddErrors(List<string> errors, List<DiagramZone> zones, List<DiagramItem> items, List<DiagramZoneKey>? keys)
    {
        var keyList = keys ?? [];
        var keyZoneIds = keyList
            .Select(x => x?.ZoneId)
            .ToList();
        var bodyZoneIds = zones
            .Select(x => x?.Id)
            .ToList();
        QuestionSchemaReader.AddIf(errors, keyList.Any(x => x is null) || keyZoneIds.Count != bodyZoneIds.Count || keyZoneIds.Distinct().Count() != keyZoneIds.Count || !keyZoneIds.All(bodyZoneIds.Contains), ErrorCodes.QuestionDiagramKeyZonesMismatch);
        var placed = keyList
            .Where(x => x is not null)
            .SelectMany(x => x.ItemIds ?? [])
            .ToList();
        var itemIds = items
            .Where(x => x?.Id is not null)
            .Select(x => x.Id!)
            .ToHashSet(StringComparer.Ordinal);
        QuestionSchemaReader.AddIf(errors, placed.Any(x => x is null || !itemIds.Contains(x)) || placed.Distinct(StringComparer.Ordinal).Count() != placed.Count, ErrorCodes.QuestionDiagramKeyItemInvalid);
        QuestionSchemaReader.AddIf(errors, keyList.Any(x => x?.ItemIds is { } ids && ids.Count > CapacityOf(zones, x.ZoneId)), ErrorCodes.QuestionDiagramZoneOverCapacity);
        QuestionSchemaReader.AddIf(errors, placed.Count == 0, ErrorCodes.QuestionDiagramKeyEmpty);
        QuestionSchemaReader.AddIf(errors, keyList.Any(x => x?.Ordered == true && (x.ItemIds?.Count ?? 0) < MinimumOrderedItems), ErrorCodes.QuestionDiagramOrderInvalid);
    }

    private static int CapacityOf(List<DiagramZone> zones, string? zoneId)
    {
        return zones.FirstOrDefault(x => x is not null && string.Equals(x.Id, zoneId, StringComparison.Ordinal))?.Capacity ?? int.MaxValue;
    }
}
