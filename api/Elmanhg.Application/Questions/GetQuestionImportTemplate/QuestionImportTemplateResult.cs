namespace Elmanhg.Application.Questions.GetQuestionImportTemplate;

public sealed record QuestionImportTemplateResult(byte[] Content, string FileName, string ContentType);
