using Elmanhg.Domain.SlaCalendars;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.SlaCalendars;

public sealed class ExamPeriodTests
{
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End = new(2026, 7, 15);
    private static readonly DateTimeOffset LongAgo = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly Guid _adminId = Guid.NewGuid();

    [Fact]
    public void Create_ValidRange_SetsFields()
    {
        var period = ExamPeriod.Create("Final exams", Start, End, _adminId);

        (period.Name, period.StartDate, period.EndDate, period.CreatedBy, period.IsDeleted).Should().Be(("Final exams", Start, End, (Guid?)_adminId, false));
        period.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_EndBeforeStart_Throws()
    {
        var act = () => ExamPeriod.Create("Final exams", End, Start, _adminId);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Update_ValidRange_SetsFieldsAndStamps()
    {
        var period = ExamPeriod.Create("Final exams", Start, End, Guid.NewGuid());
        period.UpdationDate = LongAgo;

        period.Update("Second round", Start.AddDays(10), Start.AddDays(10), _adminId);

        (period.Name, period.StartDate, period.EndDate, period.UpdatedBy).Should().Be(("Second round", Start.AddDays(10), Start.AddDays(10), (Guid?)_adminId));
        period.UpdationDate.Should().BeAfter(LongAgo);
    }

    [Fact]
    public void Update_EndBeforeStart_ThrowsAndKeepsFields()
    {
        var period = ExamPeriod.Create("Final exams", Start, End, _adminId);

        var act = () => period.Update("Second round", End, Start, Guid.NewGuid());

        act.Should().Throw<ArgumentOutOfRangeException>();
        (period.Name, period.StartDate, period.EndDate, period.UpdatedBy).Should().Be(("Final exams", Start, End, (Guid?)_adminId));
    }

    [Fact]
    public void Delete_SoftDeletesAndStamps()
    {
        var period = ExamPeriod.Create("Final exams", Start, End, Guid.NewGuid());
        period.UpdationDate = LongAgo;

        period.Delete(_adminId);

        (period.IsDeleted, period.UpdatedBy).Should().Be((true, (Guid?)_adminId));
        period.UpdationDate.Should().BeAfter(LongAgo);
    }

    [Fact]
    public void ToDateRange_ReturnsInclusiveRange()
    {
        var range = ExamPeriod.Create("Final exams", Start, End, _adminId).ToDateRange();

        (range.Start, range.End, range.Contains(Start), range.Contains(End), range.Contains(End.AddDays(1)), range.Contains(Start.AddDays(-1))).Should().Be((Start, End, true, true, false, false));
    }

    [Fact]
    public void Delete_StampsDeletedAtWithUpdationDate()
    {
        var period = ExamPeriod.Create("Final exams", Start, End, Guid.NewGuid());
        period.UpdationDate = LongAgo;

        period.Delete(_adminId);

        period.DeletedAt.Should().NotBeNull().And.Be(period.UpdationDate);
    }
}
