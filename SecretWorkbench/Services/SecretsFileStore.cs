using System.Text.Json;

namespace SecretWorkbench.Services;

/// <summary>
/// Reads and writes a user-secrets <c>secrets.json</c> file directly, so values survive
/// round-tripping exactly and a save replaces the whole set in one atomic step.
/// </summary>
/// <remarks>
/// This deliberately mirrors how <c>dotnet user-secrets</c> stores secrets: the same path (both
/// resolve it through <c>PathHelper</c>), the same write technique (a sibling temp file moved over
/// the target), and the same resulting file mode. Anything the CLI can write, this can write, and
/// the reverse holds too.
/// </remarks>
public static class SecretsFileStore
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static IReadOnlyDictionary<string, string> Read(string secretsFilePath)
    {
        if (!File.Exists(secretsFilePath))
        {
            return EmptySecrets();
        }

        string contents;
        try
        {
            contents = File.ReadAllText(secretsFilePath);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw new SecretsFileAccessException(secretsFilePath, "read", exception.Message, exception);
        }

        if (string.IsNullOrWhiteSpace(contents))
        {
            return EmptySecrets();
        }

        try
        {
            return JsonSecretFlattener.Flatten(contents);
        }
        catch (JsonException exception)
        {
            throw new JsonException($"{secretsFilePath} is not valid user-secrets JSON. {exception.Message}", exception);
        }
    }

    public static async Task WriteAsync(
        string secretsFilePath,
        IReadOnlyDictionary<string, string> secrets,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(secretsFilePath))
            ?? throw new InvalidOperationException($"The secrets path has no directory: {secretsFilePath}");

        // Write beside the target and move into place, so an interrupted save cannot
        // leave a partially written secret set behind. This is what the CLI does too.
        var temporaryPath = Path.Combine(directory, $"secrets.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(secrets, WriteOptions),
                cancellationToken);
            RestrictToCurrentUser(temporaryPath);
            File.Move(temporaryPath, secretsFilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            DeleteQuietly(temporaryPath);

            // The failure usually names the staging file, which means nothing to the user.
            var reason = exception.Message.Replace(temporaryPath, secretsFilePath, StringComparison.Ordinal);
            throw new SecretsFileAccessException(secretsFilePath, "write", reason, exception);
        }
        catch
        {
            DeleteQuietly(temporaryPath);
            throw;
        }
    }

    private static Dictionary<string, string> EmptySecrets() => new(StringComparer.OrdinalIgnoreCase);

    private static void RestrictToCurrentUser(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // The original failure is what matters.
        }
    }
}
