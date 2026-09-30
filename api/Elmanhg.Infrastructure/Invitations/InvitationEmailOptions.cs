using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Infrastructure.Invitations;

public sealed class InvitationEmailOptions
{
    public const string SectionName = "InvitationEmail";

    public string AcceptInviteUrl { get; set; } = string.Empty;

    [Required]
    public string Subject { get; set; } = "دعوة للانضمام إلى المنهج";
}
