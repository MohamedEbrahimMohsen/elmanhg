using Core.Localization;
using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Spreadsheets;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Questions.GetQuestionImportTemplate;

public sealed class GetQuestionImportTemplateHandler(ISpreadsheetWriter spreadsheetWriter, ILocalizer localizer, IOptions<ContentOptions> contentOptions) : IRequestHandler<GetQuestionImportTemplateQuery, QuestionImportTemplateResult>
{
    public Task<QuestionImportTemplateResult> Handle(GetQuestionImportTemplateQuery request, CancellationToken cancellationToken)
    {
        var sheets = QuestionImportTemplate.Build(contentOptions.Value, localizer);
        var content = spreadsheetWriter.Write(sheets);
        return Task.FromResult(new QuestionImportTemplateResult(content, QuestionImportFile.TemplateFileName, QuestionImportFile.ContentType));
    }
}
