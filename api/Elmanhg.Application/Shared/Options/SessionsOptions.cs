using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class SessionsOptions
{
    public const string SectionName = "Sessions";

    [Range(1, 100)]
    public int DefaultQuizSize { get; set; } = 10;

    [Range(1, 100)]
    public int MinQuizSize { get; set; } = 5;

    [Range(1, 100)]
    public int MaxQuizSize { get; set; } = 20;

    [Range(1, 100000)]
    public int AnswerMaxLength { get; set; } = 4000;

    [Range(1, 200000)]
    public int EssayAnswerMaxLength { get; set; } = 121000;

    public int RequestAnswerMaxLength => Math.Max(AnswerMaxLength, EssayAnswerMaxLength);
}
