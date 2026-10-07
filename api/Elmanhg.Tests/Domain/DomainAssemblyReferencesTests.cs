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
    public void DomainProject_References_OnlyCoreDddAndCoreSettings()
    {
        var project = XDocument.Load(FindDomainProject());

        var references = project.Descendants("ProjectReference").Select(x => Path.GetFileName(x.Attribute("Include")!.Value)).ToList();

        references.Should().Equal("Core.DDD.csproj", "Core.Settings.csproj");
    }

    [Fact]
    public void DomainProject_TransitiveProjects_HaveNoFrameworkReference()
    {
        var projects = CollectProjects(FindDomainProject());

        projects.Should().Contain(path => path.EndsWith("Core.Settings.csproj", StringComparison.Ordinal));
        projects.Where(path => XDocument.Load(path).Descendants("FrameworkReference").Any()).Should().BeEmpty();
    }

    private static HashSet<string> CollectProjects(string root)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>([Path.GetFullPath(root)]);
        while (pending.TryPop(out var path))
        {
            if (!visited.Add(path))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(path)!;
            foreach (var include in XDocument.Load(path).Descendants("ProjectReference").Select(x => x.Attribute("Include")!.Value))
            {
                pending.Push(Path.GetFullPath(Path.Combine(directory, include.Replace('\\',Path.DirectorySeparatorChar))));
            }
        }

        return visited;
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
