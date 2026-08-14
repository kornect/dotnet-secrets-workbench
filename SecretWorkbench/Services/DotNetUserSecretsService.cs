using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SecretWorkbench.Services;

public sealed class DotNetUserSecretsService : IUserSecretsService
{
    public Task<IReadOnlyList<string>> FindProjectsAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var fullRoot = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"Folder not found: {fullRoot}");
        }

        return Task.Run<IReadOnlyList<string>>(() =>
        {
            var projects = new List<string>();
            var pending = new Stack<string>();
            pending.Push(fullRoot);

            while (pending.Count > 0 && projects.Count < 250)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = pending.Pop();

                try
                {
                    projects.AddRange(Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly));
                    foreach (var child in Directory.EnumerateDirectories(directory))
                    {
                        var name = Path.GetFileName(child);
                        if (name is not ("bin" or "obj" or "node_modules" or ".git" or ".vs"))
                        {
                            pending.Push(child);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip folders the current user cannot inspect.
                }
            }

            return projects.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        }, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> ListAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        ValidateProject(projectPath);
        var output = await RunAsync(["user-secrets", "list", "--project", projectPath], null, cancellationToken);
        var secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Equals("No secrets configured for this application.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separator = line.IndexOf(" = ", StringComparison.Ordinal);
            if (separator > 0)
            {
                secrets[line[..separator]] = line[(separator + 3)..];
            }
        }

        return secrets;
    }

    public async Task InitializeAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        ValidateProject(projectPath);
        await RunAsync(["user-secrets", "init", "--project", projectPath], null, cancellationToken);
    }

    public async Task SaveAsync(string projectPath, IReadOnlyDictionary<string, string> secrets, CancellationToken cancellationToken = default)
    {
        ValidateProject(projectPath);
        var existing = await ListAsync(projectPath, cancellationToken);

        foreach (var removedKey in existing.Keys.Except(secrets.Keys, StringComparer.OrdinalIgnoreCase))
        {
            await RunAsync(["user-secrets", "remove", removedKey, "--project", projectPath], null, cancellationToken);
        }

        if (secrets.Count > 0)
        {
            var json = JsonSerializer.Serialize(secrets);
            await RunAsync(["user-secrets", "set", "--project", projectPath], json, cancellationToken);
        }
    }

    private static void ValidateProject(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath) ||
            !string.Equals(Path.GetExtension(projectPath), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("Choose a valid .csproj file.", projectPath);
        }
    }

    private static async Task<string> RunAsync(IReadOnlyList<string> arguments, string? standardInput, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the dotnet CLI.");
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim());
        }

        return stdout.Trim();
    }
}
