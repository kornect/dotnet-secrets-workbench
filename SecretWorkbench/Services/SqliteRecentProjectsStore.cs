using Microsoft.Data.Sqlite;

namespace SecretWorkbench.Services;

public sealed class SqliteRecentProjectsStore : IRecentProjectsStore
{
    private const int MaximumRecentProjects = 50;
    private readonly string databasePath;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private bool isInitialized;

    static SqliteRecentProjectsStore()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    public SqliteRecentProjectsStore(string? databasePath = null, TimeProvider? timeProvider = null)
    {
        this.databasePath = Path.GetFullPath(databasePath ?? DefaultDatabasePath);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public static string DefaultDatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".secrets-workbench",
        "secret-workbench.db");

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ProjectPath
            FROM RecentProjects
            ORDER BY LastOpenedUtc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", MaximumRecentProjects);

        var projects = new List<string>();
        var missingProjects = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var projectPath = reader.GetString(0);
                if (File.Exists(projectPath)) projects.Add(projectPath);
                else missingProjects.Add(projectPath);
            }
        }

        foreach (var missingProject in missingProjects)
        {
            await DeleteAsync(connection, missingProject, cancellationToken);
        }

        return projects;
    }

    public async Task RememberAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = Path.GetFullPath(projectPath);
        if (!File.Exists(normalizedPath) || !ProjectFile.IsSupported(normalizedPath))
        {
            throw new FileNotFoundException("The selected .NET project no longer exists.", normalizedPath);
        }

        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO RecentProjects (ProjectPath, LastOpenedUtc)
            VALUES ($projectPath, $lastOpenedUtc)
            ON CONFLICT(ProjectPath) DO UPDATE SET LastOpenedUtc = excluded.LastOpenedUtc;
            """;
        command.Parameters.AddWithValue("$projectPath", normalizedPath);
        command.Parameters.AddWithValue("$lastOpenedUtc", timeProvider.GetUtcNow().ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (isInitialized) return;

        await initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (isInitialized) return;
            var directory = Path.GetDirectoryName(databasePath)
                ?? throw new InvalidOperationException("The recent-project database path has no directory.");
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            await using var connection = await OpenConnectionAsync(cancellationToken);
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS RecentProjects (
                    ProjectPath TEXT NOT NULL PRIMARY KEY COLLATE NOCASE,
                    LastOpenedUtc TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            isInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task DeleteAsync(SqliteConnection connection, string projectPath, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM RecentProjects WHERE ProjectPath = $projectPath;";
        command.Parameters.AddWithValue("$projectPath", projectPath);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
