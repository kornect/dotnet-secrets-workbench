using Microsoft.Extensions.Configuration.UserSecrets;
using SecretWorkbench.Models;

namespace SecretWorkbench.Services;

/// <summary>
/// Manages a project's development secrets by reading and writing its <c>secrets.json</c> directly.
/// The <c>dotnet user-secrets</c> CLI is used only for initialization, which has to edit the project file.
/// </summary>
public sealed class DotNetUserSecretsService : IUserSecretsService
{
    public const int MaximumProjects = 250;

    private static readonly string[] SkippedDirectories = ["bin", "obj", "node_modules", ".git", ".vs"];

    private readonly UserSecretsIdResolver idResolver;
    private readonly Func<string, string> secretsPathFactory;

    public DotNetUserSecretsService(
        UserSecretsIdResolver? idResolver = null,
        Func<string, string>? secretsPathFactory = null)
    {
        this.idResolver = idResolver ?? new UserSecretsIdResolver();
        this.secretsPathFactory = secretsPathFactory ?? PathHelper.GetSecretsPathFromSecretsId;
    }

    public Task<ProjectScanResult> FindProjectsAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var fullRoot = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"Folder not found: {fullRoot}");
        }

        return Task.Run(() =>
        {
            var projects = new List<string>();
            var wasTruncated = false;

            // Breadth-first, so hitting the limit keeps the projects nearest the scan root
            // rather than an arbitrary slice of one deep branch.
            var pending = new Queue<string>();
            pending.Enqueue(fullRoot);

            while (pending.Count > 0 && !wasTruncated)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = pending.Dequeue();

                try
                {
                    foreach (var project in Directory.EnumerateFiles(directory)
                                 .Where(ProjectFile.IsSupported)
                                 .Order(StringComparer.OrdinalIgnoreCase))
                    {
                        if (projects.Count == MaximumProjects)
                        {
                            wasTruncated = true;
                            break;
                        }

                        projects.Add(project);
                    }

                    if (wasTruncated) break;

                    foreach (var child in Directory.EnumerateDirectories(directory))
                    {
                        if (!SkippedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                        {
                            pending.Enqueue(child);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip folders the current user cannot inspect.
                }
            }

            return new ProjectScanResult(projects.Order(StringComparer.OrdinalIgnoreCase).ToArray(), wasTruncated);
        }, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> ListAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        ValidateProject(projectPath);
        return SecretsFileStore.Read(await SecretsPathAsync(projectPath, cancellationToken));
    }

    public async Task InitializeAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        ValidateProject(projectPath);
        await DotNetCli.RunAsync(["user-secrets", "init", "--project", projectPath], null, cancellationToken);
        idResolver.Invalidate(projectPath);
    }

    public async Task SaveAsync(string projectPath, IReadOnlyDictionary<string, string> secrets, CancellationToken cancellationToken = default)
    {
        ValidateProject(projectPath);
        await SecretsFileStore.WriteAsync(await SecretsPathAsync(projectPath, cancellationToken), secrets, cancellationToken);
    }

    private async Task<string> SecretsPathAsync(string projectPath, CancellationToken cancellationToken) =>
        secretsPathFactory(await idResolver.ResolveAsync(projectPath, cancellationToken));

    private static void ValidateProject(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath) || !ProjectFile.IsSupported(projectPath))
        {
            throw new FileNotFoundException(
                $"Choose a valid project file ({ProjectFile.SupportedExtensionsForDisplay}).",
                projectPath);
        }
    }
}
