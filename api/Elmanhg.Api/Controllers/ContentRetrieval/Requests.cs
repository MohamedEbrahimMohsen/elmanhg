namespace Elmanhg.Api.Controllers.ContentRetrieval;

public sealed record SearchLessonContentRequest(string Query, int? Top, bool? IncludeQuestionExplanations);
