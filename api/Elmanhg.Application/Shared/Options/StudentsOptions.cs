using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class StudentsOptions
{
    public const string SectionName = "Students";

    [Range(1, 1000)]
    public int SubjectInterestsMaxCount { get; set; } = 50;
}
