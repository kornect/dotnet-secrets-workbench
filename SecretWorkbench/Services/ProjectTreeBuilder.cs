using SecretWorkbench.Models;

namespace SecretWorkbench.Services;

/// <summary>
/// Turns a flat list of project paths into the folder tree the project browser renders.
/// Folders sort before projects at each level, then alphabetically.
/// </summary>
public static class ProjectTreeBuilder
{
    public static IReadOnlyList<ProjectTreeNode> Build(string rootPath, IEnumerable<string> projects)
    {
        var root = new ProjectTreeNode(string.Empty);

        foreach (var project in projects.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var segments = Path.GetRelativePath(rootPath, project)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Where(segment => !string.IsNullOrWhiteSpace(segment) && segment != ".")
                .ToArray();

            var current = root;
            foreach (var directory in segments[..Math.Max(0, segments.Length - 1)])
            {
                var child = current.Children.FirstOrDefault(node =>
                    !node.IsProject && node.Name.Equals(directory, StringComparison.OrdinalIgnoreCase));
                if (child is null)
                {
                    child = new ProjectTreeNode(directory);
                    current.Children.Add(child);
                }

                current = child;
            }

            current.Children.Add(new ProjectTreeNode(segments.LastOrDefault() ?? Path.GetFileName(project), project));
        }

        Sort(root.Children);
        return root.Children;
    }

    private static void Sort(List<ProjectTreeNode> nodes)
    {
        nodes.Sort((left, right) => left.IsProject == right.IsProject
            ? StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name)
            : left.IsProject ? 1 : -1);

        foreach (var node in nodes) Sort(node.Children);
    }
}
