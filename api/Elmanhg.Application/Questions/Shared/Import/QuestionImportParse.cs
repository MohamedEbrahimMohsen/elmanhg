namespace Elmanhg.Application.Questions.Shared.Import;

public sealed record QuestionImportRow(string Sheet, int Row, QuestionFields Fields);

public sealed record QuestionImportParse(IReadOnlyList<QuestionImportRow> Rows, IReadOnlyList<QuestionImportRowError> Errors, int TotalRows);
