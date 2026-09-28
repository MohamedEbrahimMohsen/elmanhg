using Elmanhg.Domain.Questions.Schemas;

namespace Elmanhg.Domain.Questions.Grading;

internal static class ArabicCharacters
{
    // PRD §6.2 character classes; Unicode code points are fixed invariants.
    internal const char Tatweel = '\u0640';
    internal const char FathatanFirst = '\u064B';
    internal const char DiacriticLast = '\u065F';
    internal const char SuperscriptAlef = '\u0670';
    internal const char QuranicMarkFirst = '\u06D6';
    internal const char QuranicMarkLast = '\u06ED';
    internal const char ArabicIndicZero = '\u0660';
    internal const char ArabicIndicNine = '\u0669';
    internal const char ExtendedZero = '\u06F0';
    internal const char ExtendedNine = '\u06F9';
    internal const char Alef = '\u0627';
    internal const char AlefHamzaAbove = '\u0623';
    internal const char AlefHamzaBelow = '\u0625';
    internal const char AlefMadda = '\u0622';
    internal const char AlefWasla = '\u0671';
    internal const char TaaMarbuta = '\u0629';
    internal const char Haa = '\u0647';
    internal const char AlefMaqsura = '\u0649';
    internal const char Yaa = '\u064A';
    internal const char FarsiYeh = '\u06CC';
    internal const char ArabicComma = '\u060C';
    internal const char ArabicLetterMark = '\u061C';
    internal const char ZeroWidthSpace = '\u200B';
    internal const char RightToLeftMark = '\u200F';
    internal const char LeftToRightEmbedding = '\u202A';
    internal const char RightToLeftOverride = '\u202E';
    internal const char WordJoiner = '\u2060';
    internal const char LeftToRightIsolate = '\u2066';
    internal const char PopDirectionalIsolate = '\u2069';
    internal const char ByteOrderMark = '\uFEFF';
    internal const char ByteSwappedBom = '\uFFFE';

    internal static bool IsTashkeel(char character) => character is (>= FathatanFirst and <= DiacriticLast) or SuperscriptAlef or (>= QuranicMarkFirst and <= QuranicMarkLast);

    internal static bool IsInvisibleControl(char character) => character is ArabicLetterMark or (>= ZeroWidthSpace and <= RightToLeftMark) or (>= LeftToRightEmbedding and <= RightToLeftOverride) or WordJoiner or (>= LeftToRightIsolate and <= PopDirectionalIsolate) or ByteOrderMark;

    internal static char Map(char character, AnswerNormalization rules) => character switch
    {
        >= ArabicIndicZero and <= ArabicIndicNine when rules.ConvertDigits => (char)('0' + (character - ArabicIndicZero)),
        >= ExtendedZero and <= ExtendedNine when rules.ConvertDigits => (char)('0' + (character - ExtendedZero)),
        ArabicComma => ',',
        FarsiYeh => Yaa,
        AlefHamzaAbove or AlefHamzaBelow or AlefMadda or AlefWasla when rules.UnifyAlef => Alef,
        TaaMarbuta when rules.UnifyTaaMarbuta => Haa,
        AlefMaqsura when rules.UnifyAlefMaqsura => Yaa,
        _ => character,
    };
}
