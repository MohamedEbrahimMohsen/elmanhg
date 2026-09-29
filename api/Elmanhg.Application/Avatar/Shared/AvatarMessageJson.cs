using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Avatar.Shared;

public static class AvatarMessageJson
{
    public static string WriteContext(AiContextBundle bundle, IReadOnlyList<AiChatSource> sources) => JsonSerializer.Serialize(new AvatarMessageContext(bundle, sources), QuestionJson.SerializerOptions);

    public static AvatarMessageContext ReadContext(string json) => JsonSerializer.Deserialize<AvatarMessageContext>(json, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Avatar message context is empty.");

    public static string WriteCitations(IReadOnlyList<AvatarCitationResult> citations) => JsonSerializer.Serialize(citations, QuestionJson.SerializerOptions);

    public static List<AvatarCitationResult> ReadCitations(string json) => JsonSerializer.Deserialize<List<AvatarCitationResult>>(json, QuestionJson.SerializerOptions) ?? [];
}
