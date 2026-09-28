using Elmanhg.Domain.Questions.Schemas;
using System.Text;

namespace Elmanhg.Domain.Questions.Grading;

public static class AnswerNormalizer
{
    public static string Normalize(string? text, AnswerNormalization rules)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var composed = DropBeforeComposition(text).Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(composed.Length);
        var pendingSpace = false;
        foreach (var character in composed)
        {
            if (IsDropped(character, rules))
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (rules.CollapseWhitespace)
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                builder.Append(character);
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            var mapped = ArabicCharacters.Map(character, rules);
            builder.Append(rules.FoldCase ? char.ToLowerInvariant(mapped) : mapped);
        }

        return builder.ToString().Trim();
    }

    private static bool IsDropped(char character, AnswerNormalization rules) => (rules.StripTashkeel && ArabicCharacters.IsTashkeel(character)) || (rules.StripTatweel && character == ArabicCharacters.Tatweel);

    // string.Normalize throws on a lone surrogate and on U+FFFE; a pasted answer must never fail grading.
    // Invisible controls go before NFC so they cannot keep a letter apart from its combining mark.
    private static string DropBeforeComposition(string text)
    {
        if (!text.Any(character => char.IsSurrogate(character) || IsDroppedBeforeComposition(character)))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                builder.Append(text[index]).Append(text[index + 1]);
                index++;
            }
            else if (!char.IsSurrogate(text[index]) && !IsDroppedBeforeComposition(text[index]))
            {
                builder.Append(text[index]);
            }
        }

        return builder.ToString();
    }

    private static bool IsDroppedBeforeComposition(char character) => character == ArabicCharacters.ByteSwappedBom || ArabicCharacters.IsInvisibleControl(character);
}
