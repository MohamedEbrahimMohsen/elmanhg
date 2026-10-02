using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.SlaCalendars.CreateExamPeriod;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.SlaCalendars.CreateExamPeriod;

public sealed class CreateExamPeriodValidatorTests
{
    private static readonly DateOnly Start = new(2026, 6, 1);
    private readonly CreateExamPeriodValidator _validator = new(Options.Create(new SlaCalendarOptions()));

    [Fact]
    public void Validate_Valid_Passes()
    {
        Codes(new CreateExamPeriodCommand("Final exams", Start, Start.AddDays(44))).Should().BeEmpty();
    }

    [Fact]
    public void Validate_BlankName_FailsNameRequired()
    {
        Codes(new CreateExamPeriodCommand("   ", Start, Start)).Should().Equal(ErrorCodes.ExamPeriodNameRequired);
    }

    [Fact]
    public void Validate_NameOverMax_FailsNameTooLong()
    {
        Codes(new CreateExamPeriodCommand(new string('a', 101), Start, Start)).Should().Equal(ErrorCodes.ExamPeriodNameTooLong);
    }

    [Fact]
    public void Validate_MissingStart_FailsStartDateRequired()
    {
        Codes(new CreateExamPeriodCommand("Final exams", null, Start)).Should().Equal(ErrorCodes.ExamPeriodStartDateRequired);
    }

    [Fact]
    public void Validate_MissingEnd_FailsEndDateRequired()
    {
        Codes(new CreateExamPeriodCommand("Final exams", Start, null)).Should().Equal(ErrorCodes.ExamPeriodEndDateRequired);
    }

    [Fact]
    public void Validate_EndBeforeStart_FailsDateRangeInvalid()
    {
        Codes(new CreateExamPeriodCommand("Final exams", Start, Start.AddDays(-1))).Should().Equal(ErrorCodes.ExamPeriodDateRangeInvalid);
    }

    [Fact]
    public void Validate_SameDay_Passes()
    {
        Codes(new CreateExamPeriodCommand("Final exams", Start, Start)).Should().BeEmpty();
    }

    [Fact]
    public void Validate_OverMaxDays_FailsTooLong()
    {
        Codes(new CreateExamPeriodCommand("Final exams", Start, Start.AddDays(120))).Should().Equal(ErrorCodes.ExamPeriodTooLong);
    }

    [Fact]
    public void Validate_ExactlyMaxDays_Passes()
    {
        Codes(new CreateExamPeriodCommand("Final exams", Start, Start.AddDays(119))).Should().BeEmpty();
    }

    private List<string> Codes(CreateExamPeriodCommand command) => _validator.Validate(command).Errors.Select(x => x.ErrorCode).ToList();
}
