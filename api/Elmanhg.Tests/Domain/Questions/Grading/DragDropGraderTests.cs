using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class DragDropGraderTests
{
    private static readonly DragDropGradingSpec Spec = new([new DiagramZoneKey("z1", ["i1", "i2"], false), new DiagramZoneKey("z2", ["i4", "i3"], true)]);

    [Fact]
    public void Grade_EveryKeyedItemInPlace_ReturnsOneWithoutFeedback()
    {
        var grade = DragDropGrader.Grade(Spec, Answer(("z1", ["i1", "i2"]), ("z2", ["i4", "i3"])));

        grade.Should().Be(new NormalisedGrade(1m, null));
    }

    [Fact]
    public void Grade_NoPlacements_ReturnsUnanswered()
    {
        var grade = DragDropGrader.Grade(Spec, new DragDropAnswer(null));

        grade.Value.Should().Be(0m);
        grade.Feedback.Should().Be(GradeFeedback.Unanswered);
    }

    [Fact]
    public void Grade_OnlyEmptyOrUnknownZones_ReturnsUnanswered()
    {
        var grade = DragDropGrader.Grade(Spec, Answer(("z1", []), ("zz", ["i1"])));

        grade.Should().Be(NormalisedGrade.Unanswered);
    }

    [Fact]
    public void Grade_HalfOfKeyedItemsRight_ReturnsHalfWithPlacementTally()
    {
        var grade = DragDropGrader.Grade(Spec, Answer(("z1", ["i1", "i2"])));

        grade.Should().Be(new NormalisedGrade(0.5m, GradeFeedback.PlacementTally(2, 0, 4)));
    }

    [Fact]
    public void Grade_DistractorPlaced_CancelsOneRightItem()
    {
        var grade = DragDropGrader.Grade(Spec, Answer(("z1", ["i1", "i2", "i5"]), ("z2", ["i4", "i3"])));

        grade.Should().Be(new NormalisedGrade(0.75m, GradeFeedback.PlacementTally(4, 1, 4)));
    }

    [Fact]
    public void Grade_MoreWrongThanRight_FloorsAtZero()
    {
        var grade = DragDropGrader.Grade(Spec, Answer(("z1", ["i5", "x9"]), ("z2", ["i4"])));

        grade.Should().Be(new NormalisedGrade(0m, GradeFeedback.PlacementTally(1, 2, 4)));
    }

    [Fact]
    public void Grade_OrderedZoneSwapped_CountsNoItemOfThatZone()
    {
        var grade = DragDropGrader.Grade(Spec, Answer(("z1", ["i2", "i1"]), ("z2", ["i3", "i4"])));

        grade.Should().Be(new NormalisedGrade(0.5m, GradeFeedback.PlacementTally(2, 0, 4)));
    }

    [Fact]
    public void Grade_UnknownItemId_CountsAsWrong()
    {
        var grade = DragDropGrader.Grade(Spec, Answer(("z1", ["i1", "i2"]), ("z2", ["i4", "i3", "zz"])));

        grade.Should().Be(new NormalisedGrade(0.75m, GradeFeedback.PlacementTally(4, 1, 4)));
    }

    [Fact]
    public void Grade_ItemPlacedTwice_CountsFirstPlacementOnly()
    {
        var grade = DragDropGrader.Grade(Spec, Answer(("z1", ["i4"]), ("z2", ["i4", "i3"])));

        grade.Should().Be(new NormalisedGrade(0m, GradeFeedback.PlacementTally(0, 0, 4)));
    }

    [Fact]
    public void Grade_RepeatedZoneEntry_UsesFirstEntry()
    {
        var grade = DragDropGrader.Grade(Spec, Answer(("z1", ["i1"]), ("z1", ["i2"])));

        grade.Should().Be(new NormalisedGrade(0.25m, GradeFeedback.PlacementTally(1, 0, 4)));
    }

    [Fact]
    public void Grade_KeyedItemInWrongZone_IsNeitherRightNorWrong()
    {
        var grade = DragDropGrader.Grade(Spec, Answer(("z2", ["i1"])));

        grade.Should().Be(new NormalisedGrade(0m, GradeFeedback.PlacementTally(0, 0, 4)));
    }

    private static DragDropAnswer Answer(params (string ZoneId, List<string?> ItemIds)[] placements) => new(placements
        .Select(x => new DiagramPlacement(x.ZoneId, x.ItemIds))
        .ToList<DiagramPlacement?>());
}
