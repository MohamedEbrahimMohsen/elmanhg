using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.CreateTeacherThread;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.CreateTeacherThread;

public sealed class CreateTeacherThreadValidatorTests
{
    private readonly CreateTeacherThreadValidator _validator = new(Options.Create(new AskTeacherOptions()), new FakeRuntimeSettings());

    [Fact]
    public async Task Validate_TextWithOneContext_Passes()
    {
        var result = await _validator.ValidateAsync(Command(image: File("photo.png", "image/png", 16)), TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_BlankText_FailsTextRequired()
    {
        var result = await _validator.ValidateAsync(Command(text: "   "), TestContext.Current.CancellationToken);

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadTextRequired);
    }

    [Fact]
    public async Task Validate_TextOverMax_FailsTextTooLong()
    {
        var result = await _validator.ValidateAsync(Command(text: new string('a', 2001)), TestContext.Current.CancellationToken);

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadTextTooLong);
    }

    [Fact]
    public async Task Validate_NoContext_FailsContextInvalid()
    {
        var result = await _validator.ValidateAsync(new CreateTeacherThreadCommand("Why?", null, null, null, null), TestContext.Current.CancellationToken);

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadContextInvalid);
    }

    [Fact]
    public async Task Validate_TwoContexts_FailsContextInvalid()
    {
        var result = await _validator.ValidateAsync(new CreateTeacherThreadCommand("Why?", Guid.NewGuid(), null, Guid.NewGuid(), null), TestContext.Current.CancellationToken);

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadContextInvalid);
    }

    [Fact]
    public async Task Validate_EmptyGuidContext_FailsContextInvalid()
    {
        var result = await _validator.ValidateAsync(new CreateTeacherThreadCommand("Why?", null, Guid.Empty, null, null), TestContext.Current.CancellationToken);

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadContextInvalid);
    }

    [Fact]
    public async Task Validate_SvgExtension_FailsImageTypeInvalid()
    {
        var result = await _validator.ValidateAsync(Command(image: File("photo.svg", "image/png", 4)), TestContext.Current.CancellationToken);

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadImageTypeInvalid);
    }

    [Fact]
    public async Task Validate_PngNameWithTextContentType_FailsImageTypeInvalid()
    {
        var result = await _validator.ValidateAsync(Command(image: File("photo.png", "text/html", 4)), TestContext.Current.CancellationToken);

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadImageTypeInvalid);
    }

    [Fact]
    public async Task Validate_ImageOverMaxSize_FailsImageTooLarge()
    {
        var result = await _validator.ValidateAsync(Command(image: File("photo.png", "image/png", (5 * 1024 * 1024) + 1)), TestContext.Current.CancellationToken);

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherThreadImageTooLarge);
    }

    [Fact]
    public async Task Validate_HtmlBytesNamedPng_FailsImageTypeInvalid()
    {
        var result = await _validator.ValidateAsync(Command(image: File("photo.png", "image/png", 16, "<html><script>"u8.ToArray())), TestContext.Current.CancellationToken);

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
