using System.Text.Json;

namespace Elmanhg.Api.Controllers.Sessions;

public sealed record SubmitAnswerRequest(Guid QuestionId, JsonElement Answer, int? TimeTakenMilliseconds);
