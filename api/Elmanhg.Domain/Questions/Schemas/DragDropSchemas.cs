namespace Elmanhg.Domain.Questions.Schemas;

public sealed record DiagramImage(string? Key, int? Width, int? Height, string? Alt);

public sealed record DiagramZone(string? Id, double? X, double? Y, double? Width, double? Height, int? Capacity);

public sealed record DiagramItem(string? Id, string? Text);

public sealed record DragDropBody(DiagramImage? Image, List<DiagramZone>? Zones, List<DiagramItem>? Items);

public sealed record DiagramZoneKey(string? ZoneId, List<string>? ItemIds, bool? Ordered);

public sealed record DragDropGradingSpec(List<DiagramZoneKey>? Zones);
