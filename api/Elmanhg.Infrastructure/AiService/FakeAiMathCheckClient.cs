using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Questions.Grading;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace Elmanhg.Infrastructure.AiService;

public sealed class FakeAiMathCheckClient(IHostEnvironment hostEnvironment) : IAiMathCheckClient
{
    private const string LeftDelimiter = "\\left";
    private const string RightDelimiter = "\\right";
    private const int DigitCount = 10;
    private const char ArabicIndicZero = (char)0x0660;
    private const char ExtendedArabicIndicZero = (char)0x06F0;

    public Task<AiMathCheckResult> CheckAsync(AiMathCheckRequest request, CancellationToken cancellationToken)
    {
        if (hostEnvironment.IsProduction())
        {
            throw new ServiceUnavailableCoreException(ErrorCodes.MathCheckUnavailable);
        }

        var answer = Normalize(request.Answer);
        var index = request.Expected
            .Select(Normalize)
            .ToList()
            .FindIndex(x => string.Equals(x, answer, StringComparison.Ordinal));
        var verdict = index >= 0 ? MathAnswerVerdict.Equivalent : MathAnswerVerdict.NotEquivalent;
        return Task.FromResult(new AiMathCheckResult(verdict, index >= 0 ? index : null, []));
    }

    private static string Normalize(string latex)
    {
        var text = latex
            .Replace(LeftDelimiter, string.Empty, StringComparison.Ordinal)
            .Replace(RightDelimiter, string.Empty, StringComparison.Ordinal);
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Where(x => !char.IsWhiteSpace(x)))
        {
            builder.Append(ToLatinDigit(character));
        }

        return builder.ToString();
    }

    private static char ToLatinDigit(char character) => character switch
    {
        >= ArabicIndicZero and < (char)(ArabicIndicZero + DigitCount) => (char)('0' + (character - ArabicIndicZero)),
        >= ExtendedArabicIndicZero and < (char)(ExtendedArabicIndicZero + DigitCount) => (char)('0' + (character - ExtendedArabicIndicZero)),
        _ => character,
    };
}
