using SecretWorkbench.Services;

namespace SecretWorkbench.Tests;

public sealed class DotNetUserSecretsServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"secret-workbench-{Guid.NewGuid():N}");

    [Fact]
    public async Task FindProjectsSkipsBuildFoldersAndSortsResults()
    {
        CreateProject("zeta/Zeta.csproj");
        CreateProject("alpha/Alpha.csproj");
        CreateProject("bin/Ignored.csproj");

        var scan = await CreateService().FindProjectsAsync(root);

        Assert.Collection(scan.Projects,
            first => Assert.EndsWith("Alpha.csproj", first),
            second => Assert.EndsWith("Zeta.csproj", second));
        Assert.False(scan.WasTruncated);
    }

    [Fact]
    public async Task FindProjectsIncludesFSharpAndVisualBasicProjects()
    {
        CreateProject("fs/Library.fsproj");
        CreateProject("vb/Legacy.vbproj");
        CreateProject("cs/Api.csproj");
        CreateProject("notes/Readme.md");

        var scan = await CreateService().FindProjectsAsync(root);

        Assert.Equal(
            ["Api.csproj", "Legacy.vbproj", "Library.fsproj"],
            scan.Projects.Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task FindProjectsStopsAtTheLimitAndReportsTruncation()
    {
        for (var depth = 1; depth <= 5; depth++)
        {
            var directory = string.Join('/', Enumerable.Repeat("level", depth).Select((name, index) => $"{name}{index + 1}"));
            for (var index = 0; index < 60; index++)
            {
                CreateProject($"{directory}/Project{depth}_{index:D2}.csproj");
            }
        }

        var scan = await CreateService().FindProjectsAsync(root);

        Assert.True(scan.WasTruncated);
        Assert.Equal(DotNetUserSecretsService.MaximumProjects, scan.Projects.Count);
    }

    [Fact]
    public async Task FindProjectsPrefersProjectsNearestTheScanRootWhenTruncating()
    {
        for (var depth = 1; depth <= 5; depth++)
        {
            var directory = string.Join('/', Enumerable.Repeat("level", depth).Select((name, index) => $"{name}{index + 1}"));
            for (var index = 0; index < 60; index++)
            {
                CreateProject($"{directory}/Project{depth}_{index:D2}.csproj");
            }
        }

        var scan = await CreateService().FindProjectsAsync(root);

        var names = scan.Projects.Select(Path.GetFileName).ToHashSet();
        Assert.Equal(60, names.Count(name => name!.StartsWith("Project1_")));
        Assert.True(names.Count(name => name!.StartsWith("Project5_")) < 60);
    }

    [Fact]
    public async Task FindProjectsRejectsMissingFolder()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            CreateService().FindProjectsAsync(Path.Combine(root, "missing")));
    }

    [Fact]
    public async Task ListAsyncReadsTheSecretsFileForTheProject()
    {
        var project = CreateProject("app/App.csproj", "<UserSecretsId>list-me</UserSecretsId>");
        await SecretsFileStore.WriteAsync(SecretsPathFor("list-me"), new Dictionary<string, string>
        {
            ["Api:Key"] = "value with = sign",
            ["Pem"] = "first\nsecond"
        });

        var secrets = await CreateService().ListAsync(project);

        Assert.Equal("value with = sign", secrets["Api:Key"]);
        Assert.Equal("first\nsecond", secrets["Pem"]);
    }

    [Fact]
    public async Task ListAsyncReturnsNoSecretsBeforeAnythingHasBeenSaved()
    {
        var project = CreateProject("app/App.csproj", "<UserSecretsId>nothing-yet</UserSecretsId>");

        Assert.Empty(await CreateService().ListAsync(project));
    }

    [Fact]
    public async Task ListAsyncReportsAProjectThatHasNoUserSecretsId()
    {
        var project = CreateProject("app/App.csproj");

        await Assert.ThrowsAsync<UserSecretsNotInitializedException>(() => CreateService().ListAsync(project));
    }

    [Fact]
    public async Task SaveAsyncReplacesTheWholeSecretSetInOneStep()
    {
        var project = CreateProject("app/App.csproj", "<UserSecretsId>replace-me</UserSecretsId>");
        var service = CreateService();
        await service.SaveAsync(project, new Dictionary<string, string> { ["Keep"] = "a", ["Drop"] = "b" });

        await service.SaveAsync(project, new Dictionary<string, string> { ["Keep"] = "a", ["Added"] = "c" });

        var secrets = await service.ListAsync(project);
        Assert.Equal(["Added", "Keep"], secrets.Keys.Order().ToArray());
    }

    [Fact]
    public async Task SaveAsyncClearsEverySecretWhenTheEditorIsEmptied()
    {
        var project = CreateProject("app/App.csproj", "<UserSecretsId>clear-me</UserSecretsId>");
        var service = CreateService();
        await service.SaveAsync(project, new Dictionary<string, string> { ["Api:Key"] = "value" });

        await service.SaveAsync(project, new Dictionary<string, string>());

        Assert.Empty(await service.ListAsync(project));
    }

    [Theory]
    [InlineData("App.csproj")]
    [InlineData("App.fsproj")]
    [InlineData("App.vbproj")]
    public async Task ListAsyncAcceptsEverySupportedProjectExtension(string fileName)
    {
        var project = CreateProject($"app/{fileName}", "<UserSecretsId>any-language</UserSecretsId>");

        Assert.Empty(await CreateService().ListAsync(project));
    }

    [Fact]
    public async Task ListAsyncRejectsAFileThatIsNotAProject()
    {
        var notAProject = CreateProject("app/appsettings.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateService().ListAsync(notAProject));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private DotNetUserSecretsService CreateService() => new(secretsPathFactory: SecretsPathFor);

    private string SecretsPathFor(string secretsId) => Path.Combine(root, "usersecrets", secretsId, "secrets.json");

    private string CreateProject(string relativePath, string? propertyGroup = null)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                {propertyGroup}
              </PropertyGroup>
            </Project>
            """);
        return path;
    }
}
