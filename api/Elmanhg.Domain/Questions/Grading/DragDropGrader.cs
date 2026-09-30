using Elmanhg.Domain.Questions.Schemas;

namespace Elmanhg.Domain.Questions.Grading;

public static class DragDropGrader
{
    public static NormalisedGrade Grade(DragDropGradingSpec spec, DragDropAnswer answer)
    {
        var zoneIds = (spec.Zones ?? [])
            .Where(x => x?.ZoneId is not null)
            .Select(x => x!.ZoneId!)
            .ToHashSet(StringComparer.Ordinal);
        var keyPlaces = KeyPlaces(spec);
        var placements = Resolve(answer, zoneIds);
        if (placements.All(x => x.ItemIds.Count == 0))
        {
            return NormalisedGrade.Unanswered;
        }

        if (keyPlaces.Count == 0)
        {
            return new NormalisedGrade(0m, null);
        }

        var right = 0;
        var wrong = 0;
        foreach (var placement in placements)
        {
            for (var index = 0; index < placement.ItemIds.Count; index++)
            {
                if (!keyPlaces.TryGetValue(placement.ItemIds[index], out var place))
                {
                    wrong++;
                }
                else if (place.ZoneId == placement.ZoneId && (!place.Ordered || place.Index == index))
                {
                    right++;
                }
            }
        }

        if (right == keyPlaces.Count && wrong == 0)
        {
            return new NormalisedGrade(1m, null);
        }

        return new NormalisedGrade(Math.Max(0m, (decimal)(right - wrong) / keyPlaces.Count), GradeFeedback.PlacementTally(right, wrong, keyPlaces.Count));
    }

    private static Dictionary<string, KeyPlace> KeyPlaces(DragDropGradingSpec spec)
    {
        var places = new Dictionary<string, KeyPlace>(StringComparer.Ordinal);
        foreach (var zone in spec.Zones ?? [])
        {
            if (zone?.ZoneId is null)
            {
                continue;
            }

            var itemIds = zone.ItemIds ?? [];
            for (var index = 0; index < itemIds.Count; index++)
            {
                if (itemIds[index] is { } itemId)
                {
                    places.TryAdd(itemId, new KeyPlace(zone.ZoneId, index, zone.Ordered ?? false));
                }
            }
        }

        return places;
    }

    private static List<ZonePlacement> Resolve(DragDropAnswer answer, HashSet<string> zoneIds)
    {
        var seenZones = new HashSet<string>(StringComparer.Ordinal);
        var seenItems = new HashSet<string>(StringComparer.Ordinal);
        List<ZonePlacement> placements = [];
        foreach (var placement in answer.Placements ?? [])
        {
            if (placement?.ZoneId is null || !zoneIds.Contains(placement.ZoneId) || !seenZones.Add(placement.ZoneId))
            {
                continue;
            }

            List<string> itemIds = [];
            foreach (var itemId in placement.ItemIds ?? [])
            {
                if (itemId is not null && seenItems.Add(itemId))
                {
                    itemIds.Add(itemId);
                }
            }

            placements.Add(new ZonePlacement(placement.ZoneId, itemIds));
        }

        return placements;
    }

    private sealed record KeyPlace(string ZoneId, int Index, bool Ordered);

    private sealed record ZonePlacement(string ZoneId, List<string> ItemIds);
}
