using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.ImportQuestions;
using Elmanhg.Tests.Application.Features.Questions.Shared.Import;
using FluentAssertions;
using Microsoft.Extensions.Options;
using static Elmanhg.Tests.Application.Features.Questions.PreviewQuestionImport.PreviewQuestionImportValidatorTests;

namespace Elmanhg.Tests.Application.Features.Questions.ImportQuestions;

public sealed class ImportQuestionsValidatorTests
{
    private readonly ImportQuestionsValidator _validator = new(Options.Create(QuestionImportParserTests.ImportOptions()));

    [Fact]
    public void Validate_Valid_HasNoErrors()
    {
        _validator.Validate(new ImportQuestionsCommand(Guid.NewGuid(), Guid.NewGuid(), Upload("q.xlsx", 10))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyBatchId_HasBatchIdRequired()
    {
        Codes(new ImportQuestionsCommand(Guid.NewGuid(), Guid.Empty, Upload("q.xlsx", 10))).Should().Contain(ErrorCodes.QuestionImportBatchIdRequired);
    }

    [Fact]
    public void Validate_NullFile_HasFileRequired()
    {
        Codes(new ImportQuestionsCommand(Guid.NewGuid(), Guid.NewGuid(), null)).Should().Contain(ErrorCodes.QuestionImportFileRequired);
    }

    private List<string> Codes(ImportQuestionsCommand command) => _validator.Validate(command).Errors.Select(x => x.ErrorCode).ToList();
}
