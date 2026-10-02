using Elmanhg.Application.Avatar.DeleteMyAvatarConversation;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Avatar.DeleteMyAvatarConversation;

public sealed class DeleteMyAvatarConversationValidatorTests
{
    private readonly DeleteMyAvatarConversationValidator _validator = new();

    [Fact]
    public void Validate_Id_Passes()
    {
        var result = _validator.Validate(new DeleteMyAvatarConversationCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyId_FailsWithIdRequired()
    {
        var result = _validator.Validate(new DeleteMyAvatarConversationCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.AvatarConversationIdRequired);
    }
}
