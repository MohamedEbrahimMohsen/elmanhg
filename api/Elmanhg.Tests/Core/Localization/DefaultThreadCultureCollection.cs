namespace Elmanhg.Tests.Core.Localization;

// DefaultThreadCurrentCulture is process-wide; tests that set it must not overlap.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DefaultThreadCultureCollection
{
    public const string Name = "DefaultThreadCulture";
}
