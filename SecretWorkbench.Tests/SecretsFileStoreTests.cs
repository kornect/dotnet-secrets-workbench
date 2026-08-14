using System.Text.Json;
using SecretWorkbench.Services;

namespace SecretWorkbench.Tests;

public sealed class SecretsFileStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"secret-workbench-file-{Guid.NewGuid():N}");

    private string SecretsPath => Path.Combine(directory, "secrets.json");

    [Fact]
    public void ReadReturnsNoSecretsWhenTheFileDoesNotExist()
    {
        Assert.Empty(SecretsFileStore.Read(SecretsPath));
    }

    [Fact]
    public void ReadReturnsNoSecretsForAnEmptyFile()
    {
        WriteFile(string.Empty);

        Assert.Empty(SecretsFileStore.Read(SecretsPath));
    }

    [Fact]
    public void ReadFlattensNestedJsonThatWasEditedByHand()
    {
        WriteFile("""
            {
              "ConnectionStrings": { "Main": "Server=localhost" },
              "Services": [ { "ApiKey": "first" } ]
            }
            """);

        var secrets = SecretsFileStore.Read(SecretsPath);

        Assert.Equal("Server=localhost", secrets["ConnectionStrings:Main"]);
        Assert.Equal("first", secrets["Services:0:ApiKey"]);
    }

    [Fact]
    public void ReadPreservesValuesContainingNewlines()
    {
        WriteFile(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Certificates:Signing"] = "-----BEGIN KEY-----\nline two\n-----END KEY-----"
        }));

        var secrets = SecretsFileStore.Read(SecretsPath);

        Assert.Equal("-----BEGIN KEY-----\nline two\n-----END KEY-----", secrets["Certificates:Signing"]);
    }

    [Fact]
    public async Task WriteAsyncCreatesTheSecretsDirectoryAndFile()
    {
        await SecretsFileStore.WriteAsync(SecretsPath, new Dictionary<string, string> { ["Api:Key"] = "value" });

        Assert.True(File.Exists(SecretsPath));
        Assert.Equal("value", SecretsFileStore.Read(SecretsPath)["Api:Key"]);
    }

    [Fact]
    public async Task WriteAsyncRemovesKeysThatAreNoLongerPresent()
    {
        await SecretsFileStore.WriteAsync(SecretsPath, new Dictionary<string, string> { ["Keep"] = "a", ["Drop"] = "b" });

        await SecretsFileStore.WriteAsync(SecretsPath, new Dictionary<string, string> { ["Keep"] = "a" });

        var secrets = SecretsFileStore.Read(SecretsPath);
        Assert.Equal("a", secrets["Keep"]);
        Assert.False(secrets.ContainsKey("Drop"));
    }

    [Fact]
    public async Task WriteAsyncRoundTripsValuesThatTheCliCannotRepresent()
    {
        var awkward = new Dictionary<string, string>
        {
            ["Multiline"] = "first\nsecond",
            ["Quoted"] = "say \"hello\"",
            ["Separator"] = "looks = like = output",
            ["Empty"] = string.Empty
        };

        await SecretsFileStore.WriteAsync(SecretsPath, awkward);

        Assert.Equal(awkward, SecretsFileStore.Read(SecretsPath));
    }

    [Fact]
    public async Task WriteAsyncReplacesAnExistingFileWithoutLeavingTemporaryFilesBehind()
    {
        await SecretsFileStore.WriteAsync(SecretsPath, new Dictionary<string, string> { ["First"] = "a" });

        await SecretsFileStore.WriteAsync(SecretsPath, new Dictionary<string, string> { ["Second"] = "b" });

        Assert.Equal(["secrets.json"], Directory.GetFiles(directory).Select(Path.GetFileName));
    }

    [Fact]
    public async Task WriteAsyncRestrictsTheFileToTheCurrentUserOnUnix()
    {
        if (OperatingSystem.IsWindows()) return;

        await SecretsFileStore.WriteAsync(SecretsPath, new Dictionary<string, string> { ["Api:Key"] = "value" });

        var mode = File.GetUnixFileMode(SecretsPath);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    [Fact]
    public void ReadNamesTheFileWhenItHoldsInvalidJson()
    {
        WriteFile("{ not json");

        var error = Assert.Throws<JsonException>(() => SecretsFileStore.Read(SecretsPath));

        Assert.Contains(SecretsPath, error.Message);
    }

    [Fact]
    public void ReadExplainsWhyTheFileCouldNotBeOpened()
    {
        if (OperatingSystem.IsWindows()) return;
        WriteFile("{}");
        File.SetUnixFileMode(SecretsPath, UnixFileMode.None);

        var error = Assert.Throws<SecretsFileAccessException>(() => SecretsFileStore.Read(SecretsPath));

        Assert.Contains(SecretsPath, error.Message);
        Assert.Equal(SecretsPath, error.SecretsFilePath);
    }

    [Fact]
    public async Task WriteAsyncExplainsWhyTheFolderCouldNotBeWritten()
    {
        if (OperatingSystem.IsWindows()) return;
        WriteFile("{}");
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var error = await Assert.ThrowsAsync<SecretsFileAccessException>(() =>
            SecretsFileStore.WriteAsync(SecretsPath, new Dictionary<string, string> { ["Api:Key"] = "value" }));

        Assert.Contains(SecretsPath, error.Message);
        Assert.Contains("dotnet user-secrets", error.Message);
    }

    [Fact]
    public async Task WriteAsyncDoesNotLeakItsTemporaryFileNameIntoTheError()
    {
        if (OperatingSystem.IsWindows()) return;
        WriteFile("{}");
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var error = await Assert.ThrowsAsync<SecretsFileAccessException>(() =>
            SecretsFileStore.WriteAsync(SecretsPath, new Dictionary<string, string> { ["Api:Key"] = "value" }));

        Assert.DoesNotContain(".tmp", error.Message);
    }

    public void Dispose()
    {
        if (!Directory.Exists(directory)) return;

        // Tests deliberately remove permissions; restore them so the cleanup can run.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            foreach (var file in Directory.GetFiles(directory))
            {
                File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }

        Directory.Delete(directory, recursive: true);
    }

    private void WriteFile(string contents)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SecretsPath, contents);
    }
}
