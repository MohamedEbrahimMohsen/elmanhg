using Core.Validation.Extensions;
using FluentAssertions;
using FluentValidation;

namespace Elmanhg.Tests.Core.Validation;

public sealed class RequiredValidationExtensionsTests
{
    private readonly ProbeValidator _validator = new();

    [Fact]
    public void ValidateRequired_NullableGuidValue_Passes()
    {
        var result = _validator.Validate(new ProbeCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateRequired_NullableGuidNull_FailsGivenCode()
    {
        var result = _validator.Validate(new ProbeCommand(null));

        result.Errors.Select(x => x.ErrorCode).Should().Equal("Q");
    }

    [Fact]
    public void ValidateRequired_NullableGuidEmpty_FailsGivenCode()
    {
        var result = _validator.Validate(new ProbeCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Equal("Q");
    }

    private sealed record ProbeCommand(Guid? Key);

    private sealed class ProbeValidator : AbstractValidator<ProbeCommand>
    {
        public ProbeValidator()
        {
            RuleFor(x => x.Key).ValidateRequired("Q");
        }
    }
}
