using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.GetTeacherInbox;
using Elmanhg.Application.TeacherInbox.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.GetTeacherInbox;

public sealed class GetTeacherInboxValidatorTests
{
    private readonly GetTeacherInboxValidator _validator = new(Options.Create(new AskTeacherOptions()));

    [Fact]
    public void Validate_DefaultQuery_Passes()
    {
        var result = _validator.Validate(new GetTeacherInboxQuery());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageNumberZero_FailsWithPageNumberInvalid()
    {
        var result = _validator.Validate(new GetTeacherInboxQuery(TeacherInboxFilter.All, 0, 20));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherThreadPageNumberInvalid);
    }

    [Fact]
    public void Validate_PageSizeAboveMax_FailsWithPageSizeInvalid()
    {
        var result = _validator.Validate(new GetTeacherInboxQuery(TeacherInboxFilter.Mine, 1, 51));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherThreadPageSizeInvalid);
    }

    [Fact]
    public void Validate_UnknownFilter_FailsWithFilterInvalid()
    {
        var result = _validator.Validate(new GetTeacherInboxQuery((TeacherInboxFilter)99));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherInboxFilterInvalid);
    }

    [Fact]
    public void Validate_PageOffsetPastIntRange_FailsPageNumberInvalid()
    {
        var result = _validator.Validate(new GetTeacherInboxQuery(TeacherInboxFilter.All, int.MaxValue, 20));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadPageNumberInvalid);
    }
}
