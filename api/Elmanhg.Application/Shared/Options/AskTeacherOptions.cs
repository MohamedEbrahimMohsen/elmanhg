using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class AskTeacherOptions
{
    public const string SectionName = "AskTeacher";

    [Range(1, 20000)]
    public int QuestionTextMaxLength { get; set; } = 2000;

    [Range(1, 20)]
    public int ImageMaxSizeInMb { get; set; } = 5;

    [Range(1, 100)]
    public int ThreadListMaxPageSize { get; set; } = 50;

    [Range(1, 20000)]
    public int ReplyTextMaxLength { get; set; } = 4000;
}
