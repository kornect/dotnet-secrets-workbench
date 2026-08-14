using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;

namespace SecretWorkbench.Services;

/// <summary>
/// Resolves a project's <c>UserSecretsId</c>, preferring a direct read of the project file and
/// falling back to MSBuild evaluation when the value is imported or computed.
/// </summary>
public sealed class UserSecretsIdResolver
{
    private readonly ConcurrentDictionary<string, string> resolvedIds = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> ResolveAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        if (resolvedIds.TryGetValue(projectPath, out var cached))
        {
            return cached;
        }

        var secretsId = ReadFromProjectFile(projectPath) ?? await ReadFromMsBuildAsync(projectPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(secretsId))
        {
            throw new UserSecretsNotInitializedException(projectPath);
        }

        resolvedIds[projectPath] = secretsId;
        return secretsId;
    }

    public void Invalidate(string projectPath) => resolvedIds.TryRemove(projectPath, out _);

    /// <summary>
    /// Reads a literal <c>UserSecretsId</c> straight out of the project file. Returns <c>null</c>
    /// when the value is absent or still holds an MSBuild expression, so the caller evaluates it properly.
    /// </summary>
    private static string? ReadFromProjectFile(string projectPath)
    {
        try
        {
            return XDocument.Load(projectPath)
                .Descendants()
                .Where(element => element.Name.LocalName == "UserSecretsId")
                .Select(element => element.Value.Trim())
                .LastOrDefault(value => value.Length > 0 && !value.Contains("$(", StringComparison.Ordinal));
        }
        catch (Exception exception) when (exception is XmlException or IOException)
        {
            // Let MSBuild report what is wrong with the project instead.
            return null;
        }
    }

    private static async Task<string?> ReadFromMsBuildAsync(string projectPath, CancellationToken cancellationToken)
    {
        var output = await DotNetCli.RunAsync(
            ["msbuild", projectPath, "-getProperty:UserSecretsId", "-nologo"],
            null,
            cancellationToken);

        return output.Length == 0 ? null : output;
    }
}
