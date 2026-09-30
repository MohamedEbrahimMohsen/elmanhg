using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TrainingExports.GetTrainingExports;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.TrainingExports.GetTrainingExports;

public sealed class GetTrainingExportsValidatorTests
{
    private readonly GetTrainingExportsValidator _validator = new(Options.Create(new TrainingExportsOptions { ListMaxPageSize = 50 }));

    [Fact]
    public void Validate_ValidPage_Passes()
    {
        var result = _validator.Validate(new GetTrainingExportsQuery(2, 50));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageNumberZero_FailsPageNumberInvalid()
    {
        var result = _validator.Validate(new GetTrainingExportsQuery(0, 20));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TrainingExportPageNumberInvalid);
    }

    [Fact]
    public void Validate_PageSizeAboveMax_FailsPageSizeInvalid()
    {
        var result = _validator.Validate(new GetTrainingExportsQuery(1, 51));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TrainingExportPageSizeInvalid);
    }
}
