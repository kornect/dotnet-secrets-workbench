namespace SecretWorkbench.Models;

public sealed class ProjectTreeNode(string name, string? projectPath = null)
{
    public string Name { get; } = name;
    public string? ProjectPath { get; } = projectPath;
    public List<ProjectTreeNode> Children { get; } = [];
    public bool IsProject => ProjectPath is not null;
}
