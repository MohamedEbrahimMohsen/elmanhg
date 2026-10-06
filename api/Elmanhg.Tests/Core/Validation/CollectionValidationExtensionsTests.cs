using Core.Validation.Extensions;
using FluentAssertions;
using FluentValidation;

namespace Elmanhg.Tests.Core.Validation;

public sealed class CollectionValidationExtensionsTests
{
    private readonly ProbeValidator _validator = new();

    [Fact]
    public void ValidateDistinct_UniqueItems_Passes()
    {
        var result = _validator.Validate(new ProbeCommand([Guid.NewGuid(), Guid.NewGuid()]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateDistinct_DuplicateItems_FailsGivenCode()
    {
        var id = Guid.NewGuid();

        var result = _validator.Validate(new ProbeCommand([id, id]));

        result.Errors.Select(x => x.ErrorCode).Should().Equal("D");
    }

    [Fact]
    public void ValidateDistinct_NullCollection_Passes()
    {
        var result = _validator.Validate(new ProbeCommand(null));

        result.IsValid.Should().BeTrue();
    }

    private sealed record ProbeCommand(List<Guid>? Ids);

    private sealed class ProbeValidator : AbstractValidator<ProbeCommand>
    {
        public ProbeValidator()
        {
            RuleFor(x => x.Ids).ValidateDistinct("D");
        }
    }
}
