using SecretWorkbench.Services;

namespace SecretWorkbench.Tests;

public sealed class UserSecretsIdResolverTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"secret-workbench-id-{Guid.NewGuid():N}");
    private readonly UserSecretsIdResolver resolver = new();

    [Fact]
    public async Task ResolveAsyncReadsTheIdDeclaredInTheProjectFile()
    {
        var project = CreateProject("App.csproj", "<UserSecretsId>declared-in-project</UserSecretsId>");

        Assert.Equal("declared-in-project", await resolver.ResolveAsync(project));
    }

    [Fact]
    public async Task ResolveAsyncReadsTheIdFromAnFSharpProject()
    {
        var project = CreateProject("App.fsproj", "<UserSecretsId>fsharp-id</UserSecretsId>");

        Assert.Equal("fsharp-id", await resolver.ResolveAsync(project));
    }

    [Fact]
    public async Task ResolveAsyncFallsBackToMsBuildWhenTheIdIsInheritedFromImportedProperties()
    {
        var project = CreateProject("App.csproj", propertyGroup: null);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "Directory.Build.props"),
            "<Project><PropertyGroup><UserSecretsId>inherited-id</UserSecretsId></PropertyGroup></Project>");

        Assert.Equal("inherited-id", await resolver.ResolveAsync(project));
    }

    [Fact]
    public async Task ResolveAsyncFallsBackToMsBuildWhenTheProjectFileUsesAnUnexpandedProperty()
    {
        var project = CreateProject(
            "App.csproj",
            "<SharedSecretsId>expanded-id</SharedSecretsId><UserSecretsId>$(SharedSecretsId)</UserSecretsId>");

        Assert.Equal("expanded-id", await resolver.ResolveAsync(project));
    }

    [Fact]
    public async Task ResolveAsyncReportsThatTheProjectHasNoUserSecretsId()
    {
        var project = CreateProject("App.csproj", propertyGroup: null);

        var error = await Assert.ThrowsAsync<UserSecretsNotInitializedException>(() => resolver.ResolveAsync(project));

        Assert.Equal(project, error.ProjectPath);
    }

    [Fact]
    public async Task ResolveAsyncCachesTheIdSoRepeatedReadsDoNotRerunMsBuild()
    {
        var project = CreateProject("App.csproj", propertyGroup: null);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "Directory.Build.props"),
            "<Project><PropertyGroup><UserSecretsId>inherited-id</UserSecretsId></PropertyGroup></Project>");

        await resolver.ResolveAsync(project);
        File.Delete(Path.Combine(directory, "Directory.Build.props"));

        Assert.Equal("inherited-id", await resolver.ResolveAsync(project));
    }

    [Fact]
    public async Task InvalidateForgetsACachedIdAfterInitializationChangesTheProject()
    {
        var project = CreateProject("App.csproj", "<UserSecretsId>original-id</UserSecretsId>");
        await resolver.ResolveAsync(project);

        await File.WriteAllTextAsync(project, ProjectContents("<UserSecretsId>rewritten-id</UserSecretsId>"));
        resolver.Invalidate(project);

        Assert.Equal("rewritten-id", await resolver.ResolveAsync(project));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private string CreateProject(string fileName, string? propertyGroup)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, ProjectContents(propertyGroup));
        return path;
    }

    private static string ProjectContents(string? propertyGroup) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            {propertyGroup}
          </PropertyGroup>
        </Project>
        """;
}
