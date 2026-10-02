using System.Text.Json;

namespace Elmanhg.Api.Controllers.Configuration;

public sealed record UpdateRuntimeSettingRequest(JsonElement Value);
