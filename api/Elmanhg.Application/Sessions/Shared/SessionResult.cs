using System.Text.Json;

namespace Elmanhg.Application.Sessions.Shared;

public sealed record SessionResult(Guid Id, string Kind, JsonElement Scope, bool IsTestMode, DateTimeOffset StartedAt, DateTimeOffset? SubmittedAt, decimal? ScorePercent, long TimeTakenMilliseconds, int? CurrentPosition, List<SessionItemResult> Items);
