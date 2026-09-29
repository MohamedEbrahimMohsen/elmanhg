using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.GetMyTeacherThreads;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.GetMyTeacherThreads;

public sealed class GetMyTeacherThreadsValidatorTests
{
    private readonly GetMyTeacherThreadsValidator _validator = new(Options.Create(new AskTeacherOptions()));

    [Fact]
    public void Validate_DefaultPage_Passes()
    {
        var result = _validator.Validate(new GetMyTeacherThreadsQuery());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageNumberZero_FailsPageNumberInvalid()
    {
        var result = _validator.Validate(new GetMyTeacherThreadsQuery(0, 20));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadPageNumberInvalid);
    }

    [Fact]
    public void Validate_PageSizeOverMax_FailsPageSizeInvalid()
    {
        var result = _validator.Validate(new GetMyTeacherThreadsQuery(1, 51));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadPageSizeInvalid);
    }
}
