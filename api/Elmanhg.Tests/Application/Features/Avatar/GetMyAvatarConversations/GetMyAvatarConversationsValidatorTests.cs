using Elmanhg.Application.Avatar.GetMyAvatarConversations;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Avatar.GetMyAvatarConversations;

public sealed class GetMyAvatarConversationsValidatorTests
{
    private readonly GetMyAvatarConversationsValidator _validator = new(Options.Create(new AvatarOptions { StudentConversationsMaxPageSize = 50 }));

    [Fact]
    public void Validate_Defaults_Passes()
    {
        var result = _validator.Validate(new GetMyAvatarConversationsQuery());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageNumberZero_FailsWithPageNumberInvalid()
    {
        var result = _validator.Validate(new GetMyAvatarConversationsQuery(PageNumber: 0));

        Codes(result).Should().Contain(ErrorCodes.AvatarConversationsPageNumberInvalid);
    }

    [Fact]
    public void Validate_PageSizeZero_FailsWithPageSizeInvalid()
    {
        var result = _validator.Validate(new GetMyAvatarConversationsQuery(PageSize: 0));

        Codes(result).Should().Contain(ErrorCodes.AvatarConversationsPageSizeInvalid);
    }

    [Fact]
    public void Validate_PageSizeOverStudentMax_FailsWithPageSizeInvalid()
    {
        var result = _validator.Validate(new GetMyAvatarConversationsQuery(PageSize: 51));

        Codes(result).Should().Contain(ErrorCodes.AvatarConversationsPageSizeInvalid);
    }

    private static IEnumerable<string> Codes(ValidationResult result) => result.Errors.Select(x => x.ErrorCode);
}
