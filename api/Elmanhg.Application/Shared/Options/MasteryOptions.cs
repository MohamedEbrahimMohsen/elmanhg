using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class MasteryOptions
{
    public const string SectionName = "Mastery";

    [Range(0.01, 1.0)]
    public decimal CorrectThreshold { get; set; } = 0.8m;
}
