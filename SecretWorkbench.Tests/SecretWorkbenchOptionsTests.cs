using SecretWorkbench.Services;

namespace SecretWorkbench.Tests;

public sealed class SecretWorkbenchOptionsTests
{
    [Fact]
    public void ParseUsesSafeInteractiveDefaults()
    {
        var options = SecretWorkbenchOptions.Parse([], "/tmp/project");

        Assert.Equal(Path.GetFullPath("/tmp/project"), options.RootPath);
        Assert.Equal(0, options.Port);
        Assert.True(options.OpenBrowser);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void ParseAcceptsAllStartupOptions()
    {
        var options = SecretWorkbenchOptions.Parse(["--root", "./src", "--port", "5179", "--no-open"], "/tmp/project");

        Assert.Equal(Path.GetFullPath("./src", "/tmp/project"), options.RootPath);
        Assert.Equal(5179, options.Port);
        Assert.False(options.OpenBrowser);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void ParseRejectsInvalidPorts(string value)
    {
        Assert.Throws<ArgumentException>(() => SecretWorkbenchOptions.Parse(["--port", value]));
    }

    [Fact]
    public void ParseRejectsUnknownOptions()
    {
        Assert.Throws<ArgumentException>(() => SecretWorkbenchOptions.Parse(["--listen-anywhere"]));
    }
}
