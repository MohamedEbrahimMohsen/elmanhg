using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.PreviewQuestionImport;
using Elmanhg.Tests.Application.Features.Questions.Shared.Import;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Questions.PreviewQuestionImport;

public sealed class PreviewQuestionImportValidatorTests
{
    private readonly PreviewQuestionImportValidator _validator = new(Options.Create(QuestionImportParserTests.ImportOptions()));

    internal static FormFile Upload(string fileName, long length) => new(new MemoryStream(new byte[10]), 0, length, "file", fileName);

    [Fact]
    public void Validate_Valid_HasNoErrors()
    {
        _validator.Validate(new PreviewQuestionImportQuery(Guid.NewGuid(), Upload("q.xlsx", 10))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_HasLessonIdRequired()
    {
        Codes(new PreviewQuestionImportQuery(Guid.Empty, Upload("q.xlsx", 10))).Should().Contain(ErrorCodes.LessonIdRequired);
    }

    [Fact]
    public void Validate_NullFile_HasFileRequired()
    {
        Codes(new PreviewQuestionImportQuery(Guid.NewGuid(), null)).Should().Contain(ErrorCodes.QuestionImportFileRequired);
    }

    [Fact]
    public void Validate_CsvFile_HasFileTypeInvalid()
    {
        Codes(new PreviewQuestionImportQuery(Guid.NewGuid(), Upload("q.csv", 10))).Should().Contain(ErrorCodes.QuestionImportFileTypeInvalid);
    }

    [Fact]
    public void Validate_OversizedFile_HasFileTooLarge()
    {
        Codes(new PreviewQuestionImportQuery(Guid.NewGuid(), Upload("q.xlsx", (5 * 1024 * 1024) + 1))).Should().Contain(ErrorCodes.QuestionImportFileTooLarge);
    }

    private List<string> Codes(PreviewQuestionImportQuery query) => _validator.Validate(query).Errors.Select(x => x.ErrorCode).ToList();
}
