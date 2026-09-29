using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subscriptions.LapseSubscription;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.LapseSubscription;

public sealed class LapseSubscriptionValidatorTests
{
    private readonly LapseSubscriptionValidator _validator = new();

    [Fact]
    public void Validate_Id_Passes()
    {
        _validator.Validate(new LapseSubscriptionCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyId_FailsWithSubscriptionIdRequired()
    {
        _validator.Validate(new LapseSubscriptionCommand(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubscriptionIdRequired);
    }
}
