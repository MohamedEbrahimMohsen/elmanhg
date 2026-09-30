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

    [Range(1, 100000)]
    public int MathStepsAnswerMaxLength { get; set; } = 24000;

    [Range(1, 100)]
    public int MathStepsMaxCount { get; set; } = 20;

    [Range(1, 10000)]
    public int MathStepMaxLength { get; set; } = 500;

    [Range(1, 10000)]
    public int MathFinalAnswerMaxLength { get; set; } = 200;

    [Range(1, 100000)]
    public int DragDropAnswerMaxLength { get; set; } = 4000;

    [Range(1, 100)]
    public int DragDropPlacementsMaxCount { get; set; } = 20;

    [Range(1, 1000)]
    public int DragDropPlacedItemsMaxCount { get; set; } = 30;

    public int RequestAnswerMaxLength => Math.Max(AnswerMaxLength, Math.Max(EssayAnswerMaxLength, Math.Max(MathStepsAnswerMaxLength, DragDropAnswerMaxLength)));
}
