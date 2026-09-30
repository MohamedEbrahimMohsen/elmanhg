using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.UploadDiagramImage;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Text;

namespace Elmanhg.Tests.Application.Features.Lessons.UploadDiagramImage;

public sealed class UploadDiagramImageValidatorTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private readonly UploadDiagramImageValidator _validator = new(Options.Create(new ContentOptions { LessonImageMaxSizeInMb = 5 }));

    [Fact]
    public void Validate_PngWithPngSignature_Passes()
    {
        var result = _validator.Validate(new UploadDiagramImageCommand(Guid.NewGuid(), File("x.png", "image/png", Png)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsWithLessonIdRequired()
    {
        var result = _validator.Validate(new UploadDiagramImageCommand(Guid.Empty, File("x.png", "image/png", Png)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }

    [Fact]
    public void Validate_NullFile_FailsWithLessonImageRequired()
    {
        var result = _validator.Validate(new UploadDiagramImageCommand(Guid.NewGuid(), null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonImageRequired);
    }

    [Theory]
    [InlineData("x.svg", "image/svg+xml")]
    [InlineData("x.gif", "image/gif")]
    public void Validate_DisallowedExtension_FailsWithQuestionDiagramImageTypeInvalid(string fileName, string contentType)
    {
        var result = _validator.Validate(new UploadDiagramImageCommand(Guid.NewGuid(), File(fileName, contentType, Png)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.QuestionDiagramImageTypeInvalid);
    }

    [Fact]
    public void Validate_ContentTypeMismatch_FailsWithQuestionDiagramImageTypeInvalid()
    {
        var result = _validator.Validate(new UploadDiagramImageCommand(Guid.NewGuid(), File("x.png", "text/html", Png)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.QuestionDiagramImageTypeInvalid);
    }

    [Fact]
    public void Validate_SignatureMismatch_FailsWithQuestionDiagramImageTypeInvalid()
    {
        var result = _validator.Validate(new UploadDiagramImageCommand(Guid.NewGuid(), File("x.png", "image/png", Encoding.UTF8.GetBytes("<svg onload=x>"))));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.QuestionDiagramImageTypeInvalid);
    }

    [Fact]
    public void Validate_OverMaxSize_FailsWithLessonImageTooLarge()
    {
        var validator = new UploadDiagramImageValidator(Options.Create(new ContentOptions { LessonImageMaxSizeInMb = 1 }));
        var bytes = new byte[(1024 * 1024) + 1];
        Png.CopyTo(bytes, 0);

        var result = validator.Validate(new UploadDiagramImageCommand(Guid.NewGuid(), File("x.png", "image/png", bytes)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonImageTooLarge);
    }

    private static FormFile File(string fileName, string contentType, byte[] bytes)
    {
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }
}
