namespace Elmanhg.Application.Shared.AiService;

public sealed record AiContextBundle(AiChatEntryPoint EntryPoint, AiContextReference? Subject, AiContextReference? Unit, AiLessonContext? Lesson, AiQuestionContext? Question, IReadOnlyList<string> Subjects);
