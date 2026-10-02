namespace Elmanhg.Application.Shared.RuntimeSettings;

public sealed record RuntimeSettingConstraint(string ErrorCode, Func<RuntimeSettingValues, bool> IsSatisfiedBy);
