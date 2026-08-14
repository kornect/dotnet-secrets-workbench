using SecretWorkbench.Models;
using SecretWorkbench.Services;

namespace SecretWorkbench.Tests;

public sealed class ProjectTreeBuilderTests
{
    private const string Root = "/workspace";

    [Fact]
    public void BuildNestsProjectsUnderTheirFolders()
    {
        var tree = Build($"{Root}/src/Api/Api.csproj");

        var src = Assert.Single(tree);
        Assert.Equal("src", src.Name);
        Assert.False(src.IsProject);
        var api = Assert.Single(src.Children);
        Assert.Equal("Api", api.Name);
        var project = Assert.Single(api.Children);
        Assert.Equal("Api.csproj", project.Name);
        Assert.Equal($"{Root}/src/Api/Api.csproj", project.ProjectPath);
    }

    [Fact]
    public void BuildKeepsAProjectSittingInTheScanRootAtTheTopLevel()
    {
        var tree = Build($"{Root}/Solo.csproj");

        var project = Assert.Single(tree);
        Assert.Equal("Solo.csproj", project.Name);
        Assert.True(project.IsProject);
    }

    [Fact]
    public void BuildSharesAFolderBetweenSiblingProjects()
    {
        var tree = Build($"{Root}/src/Api/Api.csproj", $"{Root}/src/Web/Web.csproj");

        var src = Assert.Single(tree);
        Assert.Equal(["Api", "Web"], src.Children.Select(child => child.Name));
    }

    [Fact]
    public void BuildListsFoldersBeforeProjectsAtEachLevel()
    {
        var tree = Build($"{Root}/Aaa.csproj", $"{Root}/zzz/Nested.csproj");

        Assert.Equal(["zzz", "Aaa.csproj"], tree.Select(node => node.Name));
    }

    [Fact]
    public void BuildSortsFoldersAndProjectsAlphabeticallyIgnoringCase()
    {
        var tree = Build($"{Root}/beta/B.csproj", $"{Root}/Alpha/A.csproj", $"{Root}/Gamma/G.csproj");

        Assert.Equal(["Alpha", "beta", "Gamma"], tree.Select(node => node.Name));
    }

    [Fact]
    public void BuildReturnsNothingForAnEmptyProjectList()
    {
        Assert.Empty(ProjectTreeBuilder.Build(Root, []));
    }

    private static IReadOnlyList<ProjectTreeNode> Build(params string[] projects) =>
        ProjectTreeBuilder.Build(Root, projects.Select(NativePath));

    private static string NativePath(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}
