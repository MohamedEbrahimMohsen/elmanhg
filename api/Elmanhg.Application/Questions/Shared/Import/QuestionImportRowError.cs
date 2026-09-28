namespace Elmanhg.Application.Questions.Shared.Import;

public sealed record QuestionImportRowError(string Sheet, int Row, string? Column, string Code);
