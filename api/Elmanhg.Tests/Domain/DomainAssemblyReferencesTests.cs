using System.Xml.Linq;
using FluentAssertions;

namespace Elmanhg.Tests.Domain;

public sealed class DomainAssemblyReferencesTests
{
    [Fact]
    public void DomainAssembly_ReferencedAssemblies_ExcludeCoreUtilitiesAndAspNetCore()
    {
        var names = typeof(Elmanhg.Domain.Identity.User).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToList();

        names.Should().NotContain("Core.Utilities").And.NotContain(name => name!.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal));
    }

    [Fact]
    public void DomainProject_References_OnlyCoreDddAndNoFramework()
    {
        var project = XDocument.Load(FindDomainProject());

        var references = project.Descendants("ProjectReference").Select(x => Path.GetFileName(x.Attribute("Include")!.Value)).ToList();

        references.Should().Equal("Core.DDD.csproj");
        project.Descendants("FrameworkReference").Should().BeEmpty();
    }

    private static string FindDomainProject()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "api", "Elmanhg.Domain", "Elmanhg.Domain.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Elmanhg.Domain.csproj was not found above the test output directory.");
    }
}
