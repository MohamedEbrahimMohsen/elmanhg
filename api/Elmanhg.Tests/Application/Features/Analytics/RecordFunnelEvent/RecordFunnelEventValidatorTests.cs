using Elmanhg.Application.Analytics.RecordFunnelEvent;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Analytics;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Analytics.RecordFunnelEvent;

public sealed class RecordFunnelEventValidatorTests
{
    private readonly RecordFunnelEventValidator _validator = new();

    [Fact]
    public void Validate_ValidEvent_Passes()
    {
        _validator.Validate(new RecordFunnelEventCommand(Guid.NewGuid(), FunnelEventType.SignUpStarted)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyAnonymousId_FailsRequired()
    {
        Codes(new RecordFunnelEventCommand(Guid.Empty, FunnelEventType.LandingViewed)).Should().Contain(ErrorCodes.FunnelAnonymousIdRequired);
    }

    [Fact]
    public void Validate_UnknownType_FailsInvalid()
    {
        Codes(new RecordFunnelEventCommand(Guid.NewGuid(), (FunnelEventType)99)).Should().Contain(ErrorCodes.FunnelEventTypeInvalid);
    }

    private List<string> Codes(RecordFunnelEventCommand command) => _validator.Validate(command).Errors.Select(x => x.ErrorCode).ToList();
}
