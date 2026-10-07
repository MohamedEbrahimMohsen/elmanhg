using Core.Validation.Extensions;
using Core.Validation.Files;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Tests.Core.Validation;

public sealed class FileSignatureValidationTests
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly ProbeValidator _validator = new();

    [Fact]
    public void ValidateFileSignature_MatchingFile_Passes()
    {
        var result = _validator.Validate(new ProbeCommand(File("x.png", PngHeader)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateFileSignature_SpoofedFile_FailsGivenCode()
    {
        var result = _validator.Validate(new ProbeCommand(File("x.png", "<svg onload=x>"u8.ToArray())));

        result.Errors.Select(x => x.ErrorCode).Should().Equal("F");
    }

    [Fact]
    public void ValidateFileSignature_NullFile_Passes()
    {
        var result = _validator.Validate(new ProbeCommand(null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateFileSignature_EmptyFile_FailsGivenCode()
    {
        var result = _validator.Validate(new ProbeCommand(File("x.png", [])));

        result.Errors.Select(x => x.ErrorCode).Should().Equal("F");
    }

    private static FormFile File(string fileName, byte[] content) => new(new MemoryStream(content), 0, content.Length, "file", fileName);

    private sealed record ProbeCommand(IFormFile? File);

    private sealed class ProbeValidator : AbstractValidator<ProbeCommand>
    {
        public ProbeValidator()
        {
            RuleFor(x => x.File).ValidateFileSignature([FileSignature.Png], "F");
        }
    }
}
