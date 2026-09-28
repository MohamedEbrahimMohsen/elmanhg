using System.Text.Json;

namespace Elmanhg.Application.Sessions.Shared;

public sealed record SessionItemResult(int Position, Guid QuestionId, int QuestionVersion, string Type, string Stem, JsonElement Body, int MaxScore, AttemptResult? Attempt, JsonElement? CorrectAnswer, string? Explanation);
