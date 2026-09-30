using Elmanhg.Application.Users.Shared;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Users.Shared;

public sealed class ContactMaskTests
{
    [Fact]
    public void MaskPhone_ElevenDigits_KeepsFirstAndLastThree()
    {
        ContactMask.MaskPhone("01012345678").Should().Be("010*****678");
    }

    [Fact]
    public void MaskPhone_Null_ReturnsNull()
    {
        ContactMask.MaskPhone(null).Should().BeNull();
    }

    [Fact]
    public void MaskPhone_SixCharactersOrFewer_MasksEverything()
    {
        ContactMask.MaskPhone("12345").Should().Be("*****");
    }

    [Fact]
    public void MaskEmail_Address_KeepsFirstCharacterAndDomain()
    {
        ContactMask.MaskEmail("mona@example.test").Should().Be("m***@example.test");
    }

    [Fact]
    public void MaskEmail_Null_ReturnsNull()
    {
        ContactMask.MaskEmail(null).Should().BeNull();
    }

    [Fact]
    public void MaskEmail_WithoutAtSign_ReturnsMaskOnly()
    {
        ContactMask.MaskEmail("mona.example.test").Should().Be("***");
    }
}
