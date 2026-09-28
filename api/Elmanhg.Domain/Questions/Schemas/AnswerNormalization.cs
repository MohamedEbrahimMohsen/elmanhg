namespace Elmanhg.Domain.Questions.Schemas;

public sealed record AnswerNormalization(bool StripTashkeel = true, bool StripTatweel = true, bool UnifyAlef = true, bool UnifyTaaMarbuta = true, bool UnifyAlefMaqsura = true, bool ConvertDigits = true, bool CollapseWhitespace = true, bool FoldCase = true)
{
    public static AnswerNormalization Default { get; } = new();
}
