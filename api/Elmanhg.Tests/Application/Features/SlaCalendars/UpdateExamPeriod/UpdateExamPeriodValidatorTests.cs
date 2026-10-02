using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.SlaCalendars.UpdateExamPeriod;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.SlaCalendars.UpdateExamPeriod;

public sealed class UpdateExamPeriodValidatorTests
{
    private static readonly DateOnly Start = new(2026, 6, 1);
    private readonly UpdateExamPeriodValidator _validator = new(Options.Create(new SlaCalendarOptions()));

    [Fact]
    public void Validate_Valid_Passes()
    {
        Codes(Command()).Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyId_FailsIdRequired()
    {
        Codes(Command() with { ExamPeriodId = Guid.Empty }).Should().Equal(ErrorCodes.ExamPeriodIdRequired);
    }

    [Fact]
    public void Validate_BlankName_FailsNameRequired()
    {
        Codes(Command() with { Name = "" }).Should().Equal(ErrorCodes.ExamPeriodNameRequired);
    }

    [Fact]
    public void Validate_NameOverMax_FailsNameTooLong()
    {
        Codes(Command() with { Name = new string('a', 101) }).Should().Equal(ErrorCodes.ExamPeriodNameTooLong);
    }

    [Fact]
    public void Validate_MissingStart_FailsStartDateRequired()
    {
        Codes(Command() with { StartDate = null }).Should().Equal(ErrorCodes.ExamPeriodStartDateRequired);
    }

    [Fact]
    public void Validate_MissingEnd_FailsEndDateRequired()
    {
        Codes(Command() with { EndDate = null }).Should().Equal(ErrorCodes.ExamPeriodEndDateRequired);
    }

    [Fact]
    public void Validate_EndBeforeStart_FailsDateRangeInvalid()
    {
        Codes(Command() with { EndDate = Start.AddDays(-1) }).Should().Equal(ErrorCodes.ExamPeriodDateRangeInvalid);
    }

    [Fact]
    public void Validate_OverMaxDays_FailsTooLong()
    {
        Codes(Command() with { EndDate = Start.AddDays(120) }).Should().Equal(ErrorCodes.ExamPeriodTooLong);
    }

    private static UpdateExamPeriodCommand Command() => new(Guid.NewGuid(), "Final exams", Start, Start.AddDays(119));

    private List<string> Codes(UpdateExamPeriodCommand command) => _validator.Validate(command).Errors.Select(x => x.ErrorCode).ToList();
}
