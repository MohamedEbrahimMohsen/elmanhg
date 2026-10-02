using Elmanhg.Application.Avatar.GetMyAvatarConversation;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Avatar.GetMyAvatarConversation;

public sealed class GetMyAvatarConversationValidatorTests
{
    private readonly GetMyAvatarConversationValidator _validator = new();

    [Fact]
    public void Validate_Id_Passes()
    {
        var result = _validator.Validate(new GetMyAvatarConversationQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyId_FailsWithIdRequired()
    {
        var result = _validator.Validate(new GetMyAvatarConversationQuery(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.AvatarConversationIdRequired);
    }
}
