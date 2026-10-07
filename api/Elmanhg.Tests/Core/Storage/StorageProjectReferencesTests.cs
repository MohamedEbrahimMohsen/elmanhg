using System.Xml.Linq;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Storage;

public sealed class StorageProjectReferencesTests
{
    [Fact]
    public void CoreStorageProject_PackageReferences_ExcludeAwsSdk()
    {
        var project = XDocument.Load(FindProject(Path.Combine("core-libraries", "Core.Storage", "Core.Storage.csproj")));

        var packages = project.Descendants("PackageReference").Select(x => x.Attribute("Include")!.Value).ToList();

        packages.Should().NotContain(x => x.StartsWith("AWSSDK", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplicationProject_TransitiveProjects_ExcludeAwsSdk()
    {
        var projects = CollectProjects(FindProject(Path.Combine("Elmanhg.Application", "Elmanhg.Application.csproj")));

        projects.Should().Contain(path => path.EndsWith("Core.Storage.csproj", StringComparison.Ordinal));
        projects.Should().NotContain(path => path.EndsWith("Core.Storage.S3.csproj", StringComparison.Ordinal));
        projects.Where(path => XDocument.Load(path).Descendants("PackageReference").Any(x => x.Attribute("Include")!.Value.StartsWith("AWSSDK", StringComparison.OrdinalIgnoreCase))).Should().BeEmpty();
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
                pending.Push(Path.GetFullPath(Path.Combine(directory, include.Replace('\\', Path.DirectorySeparatorChar))));
            }
        }

        return visited;
    }

    private static string FindProject(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "api", relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"{relativePath} was not found above the test output directory.");
    }
}
