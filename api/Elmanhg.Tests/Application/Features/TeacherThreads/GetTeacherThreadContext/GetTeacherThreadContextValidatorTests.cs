using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherThreads.GetTeacherThreadContext;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.GetTeacherThreadContext;

public sealed class GetTeacherThreadContextValidatorTests
{
    private readonly GetTeacherThreadContextValidator _validator = new();

    [Fact]
    public void Validate_OneContext_Passes()
    {
        var result = _validator.Validate(new GetTeacherThreadContextQuery(null, null, Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NoContext_FailsContextInvalid()
    {
        var result = _validator.Validate(new GetTeacherThreadContextQuery(null, null, null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadContextInvalid);
    }
}
