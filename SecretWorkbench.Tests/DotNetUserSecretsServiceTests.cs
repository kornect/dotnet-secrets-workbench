using SecretWorkbench.Services;

namespace SecretWorkbench.Tests;

public sealed class DotNetUserSecretsServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"secret-workbench-{Guid.NewGuid():N}");

    [Fact]
    public async Task FindProjectsSkipsBuildFoldersAndSortsResults()
    {
        Directory.CreateDirectory(Path.Combine(root, "zeta"));
        Directory.CreateDirectory(Path.Combine(root, "alpha"));
        Directory.CreateDirectory(Path.Combine(root, "bin"));
        await File.WriteAllTextAsync(Path.Combine(root, "zeta", "Zeta.csproj"), "<Project />");
        await File.WriteAllTextAsync(Path.Combine(root, "alpha", "Alpha.csproj"), "<Project />");
        await File.WriteAllTextAsync(Path.Combine(root, "bin", "Ignored.csproj"), "<Project />");

        var results = await new DotNetUserSecretsService().FindProjectsAsync(root);

        Assert.Collection(results,
            first => Assert.EndsWith("Alpha.csproj", first),
            second => Assert.EndsWith("Zeta.csproj", second));
    }

    [Fact]
    public async Task FindProjectsRejectsMissingFolder()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            new DotNetUserSecretsService().FindProjectsAsync(Path.Combine(root, "missing")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
