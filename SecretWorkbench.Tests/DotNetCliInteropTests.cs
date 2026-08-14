using Microsoft.Extensions.Configuration.UserSecrets;
using SecretWorkbench.Services;

namespace SecretWorkbench.Tests;

/// <summary>
/// Secret Workbench writes <c>secrets.json</c> itself instead of shelling out to
/// <c>dotnet user-secrets set</c>. These tests pin the two directions of that contract, using the
/// real user-secrets location so the official CLI is the oracle.
/// </summary>
public sealed class DotNetCliInteropTests : IDisposable
{
    private readonly string secretsId = $"secret-workbench-interop-{Guid.NewGuid():N}";
    private readonly string workspace = Path.Combine(Path.GetTempPath(), $"secret-workbench-interop-{Guid.NewGuid():N}");

    [Fact]
    public async Task SecretsSavedByTheWorkbenchAreReadableByTheDotNetCli()
    {
        var project = CreateProject();

        await new DotNetUserSecretsService().SaveAsync(project, new Dictionary<string, string>
        {
            ["ConnectionStrings:Main"] = "Server=localhost;Database=app",
            ["Authentication:ClientSecret"] = "s3cr3t"
        });

        var listed = await DotNetCli.RunAsync(["user-secrets", "list", "--project", project]);
        Assert.Contains("ConnectionStrings:Main = Server=localhost;Database=app", listed);
        Assert.Contains("Authentication:ClientSecret = s3cr3t", listed);
    }

    [Fact]
    public async Task SecretsSetByTheDotNetCliAreReadableByTheWorkbench()
    {
        var project = CreateProject();
        await DotNetCli.RunAsync(["user-secrets", "set", "Api:Key", "from-the-cli", "--project", project]);

        var secrets = await new DotNetUserSecretsService().ListAsync(project);

        Assert.Equal("from-the-cli", secrets["Api:Key"]);
    }

    [Fact]
    public async Task TheWorkbenchWritesTheSameFileTheDotNetCliWrites()
    {
        var project = CreateProject();
        await DotNetCli.RunAsync(["user-secrets", "set", "Api:Key", "from-the-cli", "--project", project]);

        // Nothing here computes a path: the CLI chose where to write, and this is the path the
        // workbench resolves for the same project. They have to be the same file.
        var workbenchPath = PathHelper.GetSecretsPathFromSecretsId(secretsId);

        Assert.True(File.Exists(workbenchPath), $"The CLI did not write to {workbenchPath}.");
        Assert.Equal("from-the-cli", SecretsFileStore.Read(workbenchPath)["Api:Key"]);
    }

    [Fact]
    public async Task TheWorkbenchLeavesTheSameFilePermissionsTheDotNetCliDoes()
    {
        if (OperatingSystem.IsWindows()) return;
        var project = CreateProject();
        await DotNetCli.RunAsync(["user-secrets", "set", "Api:Key", "from-the-cli", "--project", project]);
        var path = PathHelper.GetSecretsPathFromSecretsId(secretsId);
        var cliMode = File.GetUnixFileMode(path);

        await new DotNetUserSecretsService().SaveAsync(project, new Dictionary<string, string> { ["Api:Key"] = "rewritten" });

        Assert.Equal(cliMode, File.GetUnixFileMode(path));
    }

    [Fact]
    public async Task SavingAnEmptySetClearsWhatTheDotNetCliReports()
    {
        var project = CreateProject();
        await DotNetCli.RunAsync(["user-secrets", "set", "Api:Key", "temporary", "--project", project]);
        var service = new DotNetUserSecretsService();

        await service.SaveAsync(project, new Dictionary<string, string>());

        var listed = await DotNetCli.RunAsync(["user-secrets", "list", "--project", project]);
        Assert.Equal("No secrets configured for this application.", listed);
    }

    public void Dispose()
    {
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);

        var secretsDirectory = Path.GetDirectoryName(PathHelper.GetSecretsPathFromSecretsId(secretsId));
        if (secretsDirectory is not null && Directory.Exists(secretsDirectory))
        {
            Directory.Delete(secretsDirectory, recursive: true);
        }
    }

    private string CreateProject()
    {
        Directory.CreateDirectory(workspace);
        var path = Path.Combine(workspace, "Interop.csproj");
        File.WriteAllText(path, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <UserSecretsId>{secretsId}</UserSecretsId>
              </PropertyGroup>
            </Project>
            """);
        return path;
    }
}
