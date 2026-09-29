using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherThreads.RateTeacherThread;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.RateTeacherThread;

public sealed class RateTeacherThreadValidatorTests
{
    private readonly RateTeacherThreadValidator _validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Validate_InRange_Passes(int rating)
    {
        var result = _validator.Validate(new RateTeacherThreadCommand(Guid.NewGuid(), rating));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Validate_OutOfRange_FailsRatingInvalid(int rating)
    {
        var result = _validator.Validate(new RateTeacherThreadCommand(Guid.NewGuid(), rating));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherThreadRatingInvalid);
    }
}
