using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class ContentOptions
{
    public const string SectionName = "Content";

    [Range(1, int.MaxValue)]
    public int SubjectNameMaxLength { get; set; }

    [Range(1, int.MaxValue)]
    public int UnitNameMaxLength { get; set; }
}
