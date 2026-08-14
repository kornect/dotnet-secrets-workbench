namespace SecretWorkbench.Services;

/// <summary>
/// Thrown when the user-secrets file exists but cannot be opened or replaced. The
/// <c>dotnet user-secrets</c> CLI writes the same file the same way, so it fails on the same
/// paths — the message says so, to keep the user from hunting for a Secret Workbench-only fault.
/// </summary>
public sealed class SecretsFileAccessException(
    string secretsFilePath,
    string action,
    string reason,
    Exception innerException)
    : IOException(
        $"Could not {action} the user-secrets file at {secretsFilePath}. {reason} " +
        $"Check that you own the containing folder; the dotnet user-secrets CLI uses the same file.",
        innerException)
{
    public string SecretsFilePath { get; } = secretsFilePath;
}
