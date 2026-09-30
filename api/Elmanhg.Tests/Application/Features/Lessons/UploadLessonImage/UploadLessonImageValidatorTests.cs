using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.UploadLessonImage;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Tests.Application.Features.TeacherThreads.CreateTeacherThread;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Lessons.UploadLessonImage;

public sealed class UploadLessonImageValidatorTests
{
    private readonly UploadLessonImageValidator _validator = new(Options.Create(Caps(5)));

    [Fact]
    public void Validate_PngImage_Passes()
    {
        var result = _validator.Validate(new UploadLessonImageCommand(Guid.NewGuid(), File("x.png", "image/png", 12)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsWithLessonIdRequired()
    {
        var result = _validator.Validate(new UploadLessonImageCommand(Guid.Empty, File("x.png", "image/png", 4)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }

    [Fact]
    public void Validate_NullFile_FailsWithLessonImageRequired()
    {
        var result = _validator.Validate(new UploadLessonImageCommand(Guid.NewGuid(), null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonImageRequired);
    }

    [Fact]
    public void Validate_SvgFile_FailsWithLessonImageTypeInvalid()
    {
        var result = _validator.Validate(new UploadLessonImageCommand(Guid.NewGuid(), File("x.svg", "image/svg+xml", 4)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonImageTypeInvalid);
    }

    [Fact]
    public void Validate_ContentTypeMismatch_FailsWithLessonImageTypeInvalid()
    {
        var result = _validator.Validate(new UploadLessonImageCommand(Guid.NewGuid(), File("x.png", "text/html", 4)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonImageTypeInvalid);
    }

    [Fact]
    public void Validate_OverMaxSize_FailsWithLessonImageTooLarge()
    {
        var validator = new UploadLessonImageValidator(Options.Create(Caps(1)));

        var result = validator.Validate(new UploadLessonImageCommand(Guid.NewGuid(), File("x.png", "image/png", (1024 * 1024) + 1)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonImageTooLarge);
    }

    [Fact]
    public void Validate_PngExtensionWithHtmlBytes_FailsWithLessonImageTypeInvalid()
    {
        var result = _validator.Validate(new UploadLessonImageCommand(Guid.NewGuid(), File("x.png", "image/png", 32, "<html><script>alert(1)</script>"u8.ToArray())));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonImageTypeInvalid);
    }

    [Fact]
    public void Validate_GifImage_Passes()
    {
        var result = _validator.Validate(new UploadLessonImageCommand(Guid.NewGuid(), File("x.gif", "image/gif", 12, "GIF89a"u8.ToArray())));

        result.IsValid.Should().BeTrue();
    }

    private static ContentOptions Caps(int imageMaxSizeInMb)
    {
        return new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = imageMaxSizeInMb };
    }

    private static FormFile File(string fileName, string contentType, long length, byte[]? header = null)
    {
        var signature = header ?? TeacherThreadImageSignatures.Png;
        var content = new byte[length];
        if (length >= signature.Length)
        {
            signature.CopyTo(content, 0);
        }

        return new FormFile(new MemoryStream(content), 0, length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }
}
