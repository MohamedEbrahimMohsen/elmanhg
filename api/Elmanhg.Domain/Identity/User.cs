using Core.DDD.Entities;
using Microsoft.AspNetCore.Identity;

namespace Elmanhg.Domain.Identity;

public class User : IdentityUser<Guid>, IAuditEntity
{
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreationDate { get; set; } = DateTimeOffset.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset UpdationDate { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
