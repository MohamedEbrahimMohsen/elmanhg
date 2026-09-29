using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class ClientErrorsOptions
{
    public const string SectionName = "ClientErrors";

    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 30;

    [Range(1, int.MaxValue)]
    public int WindowSeconds { get; set; } = 60;

    [Range(1, 10000)]
    public int MessageMaxLength { get; set; } = 500;

    [Range(1, 1000)]
    public int ErrorNameMaxLength { get; set; } = 100;

    [Range(1, 100000)]
    public int StackMaxLength { get; set; } = 4000;

    [Range(1, 2048)]
    public int PathMaxLength { get; set; } = 300;
}
