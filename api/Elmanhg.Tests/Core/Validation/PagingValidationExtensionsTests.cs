using Core.Validation;
using Core.Validation.Extensions;
using FluentAssertions;
using FluentValidation;

namespace Elmanhg.Tests.Core.Validation;

public sealed class PagingValidationExtensionsTests
{
    private const int MaxPageSize = 50;

    [Fact]
    public void ValidatePaging_ValidPage_Passes()
    {
        var result = new ProbeValidator("N", "S").Validate(new ProbeQuery(3, 20));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidatePaging_PageNumberZero_FailsPageNumberCode()
    {
        Codes(new ProbeQuery(0, 20)).Should().Equal("N");
    }

    [Fact]
    public void ValidatePaging_OffsetPastIntRange_FailsPageNumberCode()
    {
        Codes(new ProbeQuery(int.MaxValue, 20)).Should().Equal("N");
    }

    [Fact]
    public void ValidatePaging_PageSizeZero_FailsPageSizeCode()
    {
        Codes(new ProbeQuery(1, 0)).Should().Equal("S");
    }

    [Fact]
    public void ValidatePaging_PageSizeAboveMax_FailsPageSizeCode()
    {
        Codes(new ProbeQuery(1, MaxPageSize + 1)).Should().Equal("S");
    }

    [Fact]
    public void ValidatePaging_NoErrorCodes_UsesCoreDefaults()
    {
        var result = new ProbeValidator(null, null).Validate(new ProbeQuery(0, 0));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ValidationErrors.ValidationPageNumber, ValidationErrors.ValidationPageSize);
    }

    private static List<string> Codes(ProbeQuery query) => new ProbeValidator("N", "S").Validate(query).Errors.Select(x => x.ErrorCode).ToList();

    private sealed record ProbeQuery(int PageNumber, int PageSize);

    private sealed class ProbeValidator : AbstractValidator<ProbeQuery>
    {
        public ProbeValidator(string? pageNumberErrorCode, string? pageSizeErrorCode)
        {
            RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, MaxPageSize, pageNumberErrorCode, pageSizeErrorCode);
        }
    }
}
