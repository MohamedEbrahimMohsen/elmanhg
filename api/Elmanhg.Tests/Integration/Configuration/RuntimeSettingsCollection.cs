namespace Elmanhg.Tests.Integration.Configuration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RuntimeSettingsCollection
{
    public const string Name = "RuntimeSettings";
}
