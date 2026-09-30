using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TrainingExports.RequestTrainingExport;
using Elmanhg.Domain.TrainingExports;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.TrainingExports.RequestTrainingExport;

public sealed class RequestTrainingExportValidatorTests
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly RequestTrainingExportValidator _validator = new(Options.Create(new TrainingExportsOptions { MaxRangeDays = 366 }));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new RequestTrainingExportCommand(TrainingExportSource.Avatar, From, From.AddDays(30), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_UnknownSource_FailsSourceInvalid()
    {
        var result = _validator.Validate(new RequestTrainingExportCommand((TrainingExportSource)99, From, From.AddDays(1), null));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TrainingExportSourceInvalid);
    }

    [Fact]
    public void Validate_FromNotBeforeTo_FailsDateRangeInvalid()
    {
        var result = _validator.Validate(new RequestTrainingExportCommand(TrainingExportSource.Attempts, From, From, null));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TrainingExportDateRangeInvalid);
    }

    [Fact]
    public void Validate_RangeWiderThanMax_FailsDateRangeTooWide()
    {
        var result = _validator.Validate(new RequestTrainingExportCommand(TrainingExportSource.Attempts, From, From.AddDays(366).AddTicks(1), null));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TrainingExportDateRangeTooWide);
    }

    [Fact]
    public void Validate_RangeExactlyMax_Passes()
    {
        var result = _validator.Validate(new RequestTrainingExportCommand(TrainingExportSource.EssayGrades, From, From.AddDays(366), null));

        result.IsValid.Should().BeTrue();
    }
}
