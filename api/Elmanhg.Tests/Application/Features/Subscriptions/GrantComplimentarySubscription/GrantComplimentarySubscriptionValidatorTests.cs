using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subscriptions.GrantComplimentarySubscription;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.GrantComplimentarySubscription;

public sealed class GrantComplimentarySubscriptionValidatorTests
{
    private readonly GrantComplimentarySubscriptionValidator _validator = new();

    [Fact]
    public void Validate_Valid_Passes()
    {
        var result = _validator.Validate(new GrantComplimentarySubscriptionCommand(Guid.NewGuid(), SubscriptionPlan.Base, BillingPeriod.Termly));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyStudentId_FailsWithStudentIdRequired()
    {
        var result = _validator.Validate(new GrantComplimentarySubscriptionCommand(Guid.Empty, SubscriptionPlan.Base, BillingPeriod.Monthly));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.StudentIdRequired);
    }

    [Fact]
    public void Validate_UndefinedPlan_FailsWithPlanInvalid()
    {
        var result = _validator.Validate(new GrantComplimentarySubscriptionCommand(Guid.NewGuid(), (SubscriptionPlan)9, BillingPeriod.Monthly));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.ComplimentaryPlanInvalid);
    }

    [Fact]
    public void Validate_UndefinedPeriod_FailsWithPeriodInvalid()
    {
        var result = _validator.Validate(new GrantComplimentarySubscriptionCommand(Guid.NewGuid(), SubscriptionPlan.Base, (BillingPeriod)9));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.ComplimentaryPeriodInvalid);
    }
}
