using System.Text.Json.Serialization;

namespace Elmanhg.Application.Auth.Shared;

public sealed record AuthResult(string AccessToken, AuthUserResult User, [property: JsonIgnore] string RefreshToken);
