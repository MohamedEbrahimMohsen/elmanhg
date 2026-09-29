using Elmanhg.Application.Avatar.GetAvatarConversations;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Avatar;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Avatar.GetAvatarConversations;

public sealed class GetAvatarConversationsValidatorTests
{
    private static readonly DateTimeOffset From = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly GetAvatarConversationsValidator _validator = new(Options.Create(new AvatarOptions { AdminConversationsMaxPageSize = 100, ConversationSearchMaxLength = 200 }));

    [Fact]
    public void Validate_Defaults_Passes()
    {
        var result = _validator.Validate(Query());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageNumberZero_FailsWithPageNumberInvalid()
    {
        var result = _validator.Validate(Query() with { PageNumber = 0 });

        Codes(result).Should().Contain(ErrorCodes.AvatarConversationsPageNumberInvalid);
    }

    [Fact]
    public void Validate_PageSizeOverMax_FailsWithPageSizeInvalid()
    {
        var result = _validator.Validate(Query() with { PageSize = 101 });

        Codes(result).Should().Contain(ErrorCodes.AvatarConversationsPageSizeInvalid);
    }

    [Fact]
    public void Validate_PageSizeZero_FailsWithPageSizeInvalid()
    {
        var result = _validator.Validate(Query() with { PageSize = 0 });

        Codes(result).Should().Contain(ErrorCodes.AvatarConversationsPageSizeInvalid);
    }

    [Fact]
    public void Validate_SearchOverMax_FailsWithSearchTooLong()
    {
        var result = _validator.Validate(Query() with { Search = new string('q', 201) });

        Codes(result).Should().Contain(ErrorCodes.AvatarConversationsSearchTooLong);
    }

    [Fact]
    public void Validate_UndefinedEntryPoint_FailsWithEntryPointInvalid()
    {
        var result = _validator.Validate(Query() with { EntryPoint = (AvatarEntryPoint)99 });

        Codes(result).Should().Contain(ErrorCodes.AvatarConversationsEntryPointInvalid);
    }

    [Fact]
    public void Validate_FromNotBeforeTo_FailsWithDateRangeInvalid()
    {
        var result = _validator.Validate(Query() with { From = From, To = From });

        Codes(result).Should().Contain(ErrorCodes.AvatarConversationsDateRangeInvalid);
    }

    private static GetAvatarConversationsQuery Query() => new(null, null, null, null);

    private static IEnumerable<string> Codes(ValidationResult result) => result.Errors.Select(x => x.ErrorCode);
}
