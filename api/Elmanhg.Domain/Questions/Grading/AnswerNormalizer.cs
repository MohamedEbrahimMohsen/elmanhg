using System.Text;

namespace Elmanhg.Domain.Questions.Grading;

public static class AnswerNormalizer
{
    // PRD §6.2 character classes; Unicode code points are fixed invariants.
    private const char Tatweel = 'ـ';
    private const char FathatanFirst = 'ً';
    private const char DiacriticLast = 'ٟ';
    private const char SuperscriptAlef = 'ٰ';
    private const char QuranicMarkFirst = 'ۖ';
    private const char QuranicMarkLast = 'ۭ';
    private const char ArabicIndicZero = '٠';
    private const char ArabicIndicNine = '٩';
    private const char ExtendedZero = '۰';
    private const char ExtendedNine = '۹';
    private const char Alef = 'ا';
    private const char AlefHamzaAbove = 'أ';
    private const char AlefHamzaBelow = 'إ';
    private const char AlefMadda = 'آ';
    private const char AlefWasla = 'ٱ';
    private const char TaaMarbuta = 'ة';
    private const char Haa = 'ه';
    private const char AlefMaqsura = 'ى';
    private const char Yaa = 'ي';

    public static string Normalize(string? text, bool unifyLetterVariants)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (IsDiacritic(character) || character == Tatweel)
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(Map(character, unifyLetterVariants)));
        }

        return builder.ToString();
    }

    private static bool IsDiacritic(char character)
    {
        return character is (>= FathatanFirst and <= DiacriticLast) or SuperscriptAlef or (>= QuranicMarkFirst and <= QuranicMarkLast);
    }

    private static char Map(char character, bool unifyLetterVariants)
    {
        return character switch
        {
            >= ArabicIndicZero and <= ArabicIndicNine => (char)('0' + (character - ArabicIndicZero)),
            >= ExtendedZero and <= ExtendedNine => (char)('0' + (character - ExtendedZero)),
            AlefHamzaAbove or AlefHamzaBelow or AlefMadda or AlefWasla when unifyLetterVariants => Alef,
            TaaMarbuta when unifyLetterVariants => Haa,
            AlefMaqsura when unifyLetterVariants => Yaa,
            _ => character,
        };
    }
}
