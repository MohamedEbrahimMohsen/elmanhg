using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.CreateTeacherThread;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.CreateTeacherThread;

public sealed class CreateTeacherThreadValidatorTests
{
    private readonly CreateTeacherThreadValidator _validator = new(Options.Create(new AskTeacherOptions()));

    [Fact]
    public void Validate_TextWithOneContext_Passes()
    {
        var result = _validator.Validate(Command(image: File("photo.png", "image/png", 16)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BlankText_FailsTextRequired()
    {
        var result = _validator.Validate(Command(text: "   "));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadTextRequired);
    }

    [Fact]
    public void Validate_TextOverMax_FailsTextTooLong()
    {
        var result = _validator.Validate(Command(text: new string('a', 2001)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadTextTooLong);
    }

    [Fact]
    public void Validate_NoContext_FailsContextInvalid()
    {
        var result = _validator.Validate(new CreateTeacherThreadCommand("Why?", null, null, null, null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadContextInvalid);
    }

    [Fact]
    public void Validate_TwoContexts_FailsContextInvalid()
    {
        var result = _validator.Validate(new CreateTeacherThreadCommand("Why?", Guid.NewGuid(), null, Guid.NewGuid(), null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadContextInvalid);
    }

    [Fact]
    public void Validate_EmptyGuidContext_FailsContextInvalid()
    {
        var result = _validator.Validate(new CreateTeacherThreadCommand("Why?", null, Guid.Empty, null, null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadContextInvalid);
    }

    [Fact]
    public void Validate_SvgExtension_FailsImageTypeInvalid()
    {
        var result = _validator.Validate(Command(image: File("photo.svg", "image/png", 4)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadImageTypeInvalid);
    }

    [Fact]
    public void Validate_PngNameWithTextContentType_FailsImageTypeInvalid()
    {
        var result = _validator.Validate(Command(image: File("photo.png", "text/html", 4)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadImageTypeInvalid);
    }

    [Fact]
    public void Validate_ImageOverMaxSize_FailsImageTooLarge()
    {
        var result = _validator.Validate(Command(image: File("photo.png", "image/png", (5 * 1024 * 1024) + 1)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadImageTooLarge);
    }

    [Fact]
    public void Validate_HtmlBytesNamedPng_FailsImageTypeInvalid()
    {
        var result = _validator.Validate(Command(image: File("photo.png", "image/png", 16, "<html><script>"u8.ToArray())));

        result.Errors.Select(x => x.ErrorCode).Should().ContainSingle().Which.Should().Be(ErrorCodes.TeacherThreadImageTypeInvalid);
    }

    private static CreateTeacherThreadCommand Command(string text = "Why is F = ma?", IFormFile? image = null) => new(text, Guid.NewGuid(), null, null, image);

    private static FormFile File(string fileName, string contentType, long length, byte[]? header = null)
    {
        var bytes = new byte[length];
        var prefix = header ?? TeacherThreadImageSignatures.Png;
        prefix.AsSpan(0, (int)Math.Min(length, prefix.Length)).CopyTo(bytes);
        return new(new MemoryStream(bytes), 0, length, "image", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }
}
