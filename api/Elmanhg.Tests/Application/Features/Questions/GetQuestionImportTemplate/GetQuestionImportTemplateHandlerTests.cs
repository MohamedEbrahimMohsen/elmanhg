using Core.Localization;
using Elmanhg.Application.Questions.GetQuestionImportTemplate;
using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Application.Shared.Spreadsheets;
using Elmanhg.Tests.Application.Features.Questions.Shared.Import;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Questions.GetQuestionImportTemplate;

public sealed class GetQuestionImportTemplateHandlerTests
{
    [Fact]
    public async Task Handle_Default_ReturnsWriterBytesWithXlsxNameAndType()
    {
        var writer = Substitute.For<ISpreadsheetWriter>();
        IReadOnlyList<SpreadsheetSheetDefinition>? written = null;
        byte[] bytes = [1, 2, 3];
        writer.Write(Arg.Do<IReadOnlyList<SpreadsheetSheetDefinition>>(x => written = x)).Returns(bytes);
        var handler = new GetQuestionImportTemplateHandler(writer, Substitute.For<ILocalizer>(), Options.Create(QuestionImportParserTests.ImportOptions()));

        var result = await handler.Handle(new GetQuestionImportTemplateQuery(), TestContext.Current.CancellationToken);

        written.Should().HaveCount(6);
        result.Content.Should().BeSameAs(bytes);
        result.FileName.Should().Be(QuestionImportFile.TemplateFileName);
        result.ContentType.Should().Be(QuestionImportFile.ContentType);
    }
}
