using System.Text.Json;

namespace Elmanhg.Application.Sessions.Shared;

public sealed record AttemptResult(Guid Id, JsonElement Answer, decimal Score, decimal NormalisedScore, string Outcome, string? Feedback, int TimeTakenMilliseconds, DateTimeOffset CreatedAt);
