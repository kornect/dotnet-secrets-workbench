using SecretWorkbench.Services;

namespace SecretWorkbench.Tests;

public sealed class SqliteRecentProjectsStoreTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"secret-workbench-tests-{Guid.NewGuid():N}");
    private readonly ManualTimeProvider timeProvider = new();

    [Fact]
    public async Task RememberAsync_ListsMostRecentlyOpenedProjectFirst()
    {
        var store = CreateStore();
        var first = CreateProject("First");
        var second = CreateProject("Second");

        await store.RememberAsync(first);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await store.RememberAsync(second);

        Assert.Equal([second, first], await store.ListAsync());
    }

    [Fact]
    public async Task RememberAsync_UpdatesExistingProjectWithoutDuplicatingIt()
    {
        var store = CreateStore();
        var first = CreateProject("First");
        var second = CreateProject("Second");

        await store.RememberAsync(first);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await store.RememberAsync(second);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await store.RememberAsync(first);

        Assert.Equal([first, second], await store.ListAsync());
    }

    [Fact]
    public async Task ListAsync_RemovesProjectsThatNoLongerExist()
    {
        var databasePath = Path.Combine(temporaryDirectory, "data", "recent.db");
        var store = new SqliteRecentProjectsStore(databasePath, timeProvider);
        var project = CreateProject("Temporary");
        await store.RememberAsync(project);

        File.Delete(project);

        Assert.Empty(await store.ListAsync());
        Assert.Empty(await new SqliteRecentProjectsStore(databasePath, timeProvider).ListAsync());
    }

    [Fact]
    public async Task RememberAsync_CreatesDatabaseDirectory()
    {
        var databasePath = Path.Combine(temporaryDirectory, "nested", ".secrets-workbench", "secret-workbench.db");
        var store = new SqliteRecentProjectsStore(databasePath, timeProvider);

        await store.RememberAsync(CreateProject("Example"));

        Assert.True(File.Exists(databasePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
    }

    private SqliteRecentProjectsStore CreateStore() => new(
        Path.Combine(temporaryDirectory, ".secrets-workbench", "secret-workbench.db"),
        timeProvider);

    private string CreateProject(string name)
    {
        Directory.CreateDirectory(temporaryDirectory);
        var path = Path.Combine(temporaryDirectory, $"{name}.csproj");
        File.WriteAllText(path, "<Project />");
        return path;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration) => utcNow += duration;
    }
}
