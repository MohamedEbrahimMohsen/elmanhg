namespace Core.DDD.Identity;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? UserName { get; }
    string? Role { get; }
}
