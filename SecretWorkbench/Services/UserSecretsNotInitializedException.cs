namespace SecretWorkbench.Services;

/// <summary>
/// Thrown when a project has no <c>UserSecretsId</c> yet. Callers recover by running
/// initialization, rather than by matching CLI output text.
/// </summary>
public sealed class UserSecretsNotInitializedException(string projectPath)
    : InvalidOperationException($"The project has no UserSecretsId yet: {projectPath}")
{
    public string ProjectPath { get; } = projectPath;
}
