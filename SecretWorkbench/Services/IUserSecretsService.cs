using SecretWorkbench.Models;

namespace SecretWorkbench.Services;

public interface IUserSecretsService
{
    Task<ProjectScanResult> FindProjectsAsync(string rootPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> ListAsync(string projectPath, CancellationToken cancellationToken = default);
    Task InitializeAsync(string projectPath, CancellationToken cancellationToken = default);
    Task SaveAsync(string projectPath, IReadOnlyDictionary<string, string> secrets, CancellationToken cancellationToken = default);
}
