using Elmanhg.Application.Sessions.Shared;
using System.Text.Json;

namespace Elmanhg.Application.Exams.Shared;

public sealed record ExamItemResult(int Position, Guid QuestionId, int QuestionVersion, string Type, string Stem, JsonElement Body, int MaxScore, JsonElement? SavedAnswer, DateTimeOffset? AnswerSavedAt, AttemptResult? Attempt, JsonElement? CorrectAnswer, string? Explanation);
