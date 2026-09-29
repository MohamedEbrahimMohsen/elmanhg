using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subscriptions.CancelSubscription;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.CancelSubscription;

public sealed class CancelSubscriptionValidatorTests
{
    private readonly CancelSubscriptionValidator _validator = new();

    [Fact]
    public void Validate_Id_Passes()
    {
        _validator.Validate(new CancelSubscriptionCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyId_FailsWithSubscriptionIdRequired()
    {
        _validator.Validate(new CancelSubscriptionCommand(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubscriptionIdRequired);
    }
}
