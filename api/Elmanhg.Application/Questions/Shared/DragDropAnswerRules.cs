using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public static class DragDropAnswerRules
{
    public static bool CanRead(JsonElement answer) => QuestionSchemaReader.TryRead<DragDropAnswer>(answer, out var drag) && (drag.Placements is null || drag.Placements.All(x => x is not null && x.ZoneId is not null && (x.ItemIds is null || x.ItemIds.All(id => id is not null))));

    public static string Canonicalize(JsonElement answer)
    {
        var drag = QuestionSchemaReader.Read<DragDropAnswer>(answer);
        var placements = (drag.Placements ?? [])
            .Select(x => new DiagramPlacement(x!.ZoneId, (x.ItemIds ?? []).ToList()))
            .Where(x => x.ItemIds!.Count > 0)
            .ToList<DiagramPlacement?>();
        return QuestionSchemaReader.Serialize(new DragDropAnswer(placements));
    }

    public static bool ExceedsLimits(JsonElement answer, SessionsOptions options)
    {
        var placements = QuestionSchemaReader.Read<DragDropAnswer>(answer).Placements ?? [];
        return placements.Count > options.DragDropPlacementsMaxCount
            || placements.Sum(x => x?.ItemIds?.Count ?? 0) > options.DragDropPlacedItemsMaxCount;
    }
}
