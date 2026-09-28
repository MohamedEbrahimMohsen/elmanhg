using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.GetSessionHistory;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Sessions;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Progress.GetSessionHistory;

public sealed class GetSessionHistoryValidatorTests
{
    private readonly GetSessionHistoryValidator _validator = new(Options.Create(new ProgressOptions { HistoryMaxPageSize = 50 }));

    [Fact]
    public void Validate_Defaults_Passes()
    {
        var result = _validator.Validate(new GetSessionHistoryQuery(null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageNumberZero_FailsWithPageNumberInvalid()
    {
        var result = _validator.Validate(new GetSessionHistoryQuery(null, PageNumber: 0));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SessionHistoryPageNumberInvalid);
    }

    [Fact]
    public void Validate_PageSizeZero_FailsWithPageSizeInvalid()
    {
        var result = _validator.Validate(new GetSessionHistoryQuery(null, PageSize: 0));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SessionHistoryPageSizeInvalid);
    }

    [Fact]
    public void Validate_PageSizeAboveMax_FailsWithPageSizeInvalid()
    {
        var result = _validator.Validate(new GetSessionHistoryQuery(SessionHistoryKind.Quiz, PageSize: 51));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SessionHistoryPageSizeInvalid);
    }

    [Fact]
    public void Validate_KindOutOfRange_FailsWithKindInvalid()
    {
        var result = _validator.Validate(new GetSessionHistoryQuery((SessionHistoryKind)9));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SessionHistoryKindInvalid);
    }
}
