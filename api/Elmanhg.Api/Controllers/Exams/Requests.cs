using System.Text.Json;

namespace Elmanhg.Api.Controllers.Exams;

public sealed record SaveExamAnswerRequest(JsonElement Answer);

public sealed record StartMultiUnitExamRequest(List<Guid>? UnitIds, int Size);
