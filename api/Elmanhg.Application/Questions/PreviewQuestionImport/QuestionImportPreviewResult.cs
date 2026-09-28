using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.Questions.PreviewQuestionImport;

public sealed record QuestionImportPreviewResult(int TotalRows, int ValidRows, List<QuestionImportTypeCount> Types, List<QuestionImportRowError> Errors);

public sealed record QuestionImportTypeCount(QuestionType Type, int Count);
