using System.Text.Json;

namespace Elmanhg.Api.Controllers.Exams;

public sealed record SaveExamAnswerRequest(JsonElement Answer);
