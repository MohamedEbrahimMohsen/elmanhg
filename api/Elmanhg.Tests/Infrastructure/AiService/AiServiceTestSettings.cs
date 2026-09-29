using Elmanhg.Application.Shared.AiService;
using Elmanhg.Infrastructure.AiService;
using System.Globalization;

namespace Elmanhg.Tests.Infrastructure.AiService;

public static class AiServiceTestSettings
{
    public const string ServiceToken = "elmanhg-tests-ai-service-token-0123456789";

    public static AiServiceOptions Fake() => new();

    public static AiServiceOptions WithHttp()
    {
        var options = Fake();
        options.Provider = AiServiceProvider.Http;
        options.BaseUrl = "http://ai.test";
        options.ServiceToken = ServiceToken;
        return options;
    }

    public static Dictionary<string, string?> ToConfiguration(AiServiceOptions options) => new()
    {
        ["AiService:Provider"] = options.Provider.ToString(),
        ["AiService:BaseUrl"] = options.BaseUrl,
        ["AiService:ServiceToken"] = options.ServiceToken,
        ["AiService:AttemptTimeoutSeconds"] = options.AttemptTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        ["AiService:TotalTimeoutSeconds"] = options.TotalTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
    };

    public static AiChatRequest ChatRequest()
    {
        var context = new AiContextBundle(
            AiChatEntryPoint.QuizQuestion,
            new AiContextReference(Guid.Parse("0f5e2a4c-1b3d-4e6f-8a9b-0c1d2e3f4a5b"), "Physics"),
            new AiContextReference(Guid.Parse("1a2b3c4d-5e6f-4a1b-9c2d-3e4f5a6b7c8d"), "Electric current"),
            new AiLessonContext(Guid.Parse("2b3c4d5e-6f7a-4b2c-8d3e-4f5a6b7c8d9e"), "Ohm's law", "V = IR", ["Apply Ohm's law"], "R = V / I"),
            new AiQuestionContext(Guid.Parse("3c4d5e6f-7a8b-4c3d-9e4f-5a6b7c8d9e0f"), "Find R for 8 V and 2 A.", "2", "4", Explanation: null),
            []);
        return new AiChatRequest(context, [new AiChatMessage(AiChatRole.User, "q1"), new AiChatMessage(AiChatRole.Assistant, "a1")], "why?");
    }
}
