namespace Core.Settings;

public sealed record RuntimeSettingConstraint(string ErrorCode, Func<RuntimeSettingValues, bool> IsSatisfiedBy);
