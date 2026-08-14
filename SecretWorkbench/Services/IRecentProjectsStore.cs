namespace SecretWorkbench.Services;

public interface IRecentProjectsStore
{
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

    Task RememberAsync(string projectPath, CancellationToken cancellationToken = default);
}
