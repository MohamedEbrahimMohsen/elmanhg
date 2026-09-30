using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Schemas;

namespace Elmanhg.Application.Questions.Shared;

public static class DiagramZoneRules
{
    // Geometry is compared in whole hundredths of a percent (the stored precision), so float sums never cross a bound.
    private const int HundredthsPerPercent = 100;
    private const int FullImage = 100 * HundredthsPerPercent;
    // Two-decimal inputs such as 12.35 are not exact in binary; this recognises them.
    private const double DecimalTolerance = 1e-6;

    private readonly record struct Bounds(int X, int Y, int Width, int Height);

    public static void AddErrors(List<string> errors, List<DiagramZone>? zones, ContentOptions options)
    {
        var list = zones ?? [];
        var ids = list
            .Where(x => x?.Id is not null)
            .Select(x => x!.Id!)
            .ToList();
        QuestionSchemaReader.AddIf(errors, list.Count < 1 || list.Count > options.QuestionDiagramZonesMaxCount, ErrorCodes.QuestionDiagramZonesCountInvalid);
        QuestionSchemaReader.AddIf(errors, list.Any(x => x is null || !QuestionSchemaReader.IsValidId(x.Id)), ErrorCodes.QuestionDiagramZoneIdInvalid);
        QuestionSchemaReader.AddIf(errors, ids.Distinct(StringComparer.Ordinal).Count() != ids.Count, ErrorCodes.QuestionDiagramZoneIdDuplicate);
        var bounds = list
            .Select(x => ToBounds(x, options.QuestionDiagramZoneMinSizePercent))
            .ToList();
        QuestionSchemaReader.AddIf(errors, bounds.Any(x => x is null), ErrorCodes.QuestionDiagramZoneBoundsInvalid);
        QuestionSchemaReader.AddIf(errors, list.Any(x => x?.Capacity is null || x.Capacity < 1 || x.Capacity > options.QuestionDiagramZoneCapacityMax), ErrorCodes.QuestionDiagramZoneCapacityInvalid);
        QuestionSchemaReader.AddIf(errors, bounds.All(x => x is not null) && AnyOverlap(bounds.Select(x => x!.Value).ToList()), ErrorCodes.QuestionDiagramZonesOverlap);
    }

    private static bool AnyOverlap(List<Bounds> bounds)
    {
        return bounds
            .SelectMany((a, i) => bounds.Skip(i + 1), Overlaps)
            .Any(x => x);
    }

    private static Bounds? ToBounds(DiagramZone? zone, int minSizePercent)
    {
        if (zone is null || !TryHundredths(zone.X, out var x) || !TryHundredths(zone.Y, out var y) || !TryHundredths(zone.Width, out var width) || !TryHundredths(zone.Height, out var height))
        {
            return null;
        }

        var minimum = minSizePercent * HundredthsPerPercent;
        var fits = width >= minimum && height >= minimum && x + width <= FullImage && y + height <= FullImage;
        return fits ? new Bounds(x, y, width, height) : null;
    }

    private static bool TryHundredths(double? value, out int hundredths)
    {
        hundredths = 0;
        if (value is not (>= 0 and <= 100))
        {
            return false;
        }

        var scaled = value.Value * HundredthsPerPercent;
        var rounded = Math.Round(scaled);
        if (Math.Abs(scaled - rounded) > DecimalTolerance)
        {
            return false;
        }

        hundredths = (int)rounded;
        return true;
    }

    private static bool Overlaps(Bounds a, Bounds b) => a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
}
