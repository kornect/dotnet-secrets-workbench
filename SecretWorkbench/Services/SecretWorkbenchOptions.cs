namespace SecretWorkbench.Services;

public sealed record SecretWorkbenchOptions(string RootPath, int Port, bool OpenBrowser, bool ShowHelp)
{
    public const string HelpText = """
        Secret Workbench — local GUI for ASP.NET Core user secrets

        Usage:
          secret-workbench [options]

        Options:
          --root <path>  Initial folder to scan. Defaults to the current directory.
          --port <port>  Loopback port. Use 0 for an available dynamic port (default).
          --no-open      Do not open the default browser automatically.
          -h, --help     Show this help text.
        """;

    public static SecretWorkbenchOptions Parse(IReadOnlyList<string> arguments, string? currentDirectory = null)
    {
        var workingDirectory = currentDirectory ?? Directory.GetCurrentDirectory();
        var rootPath = workingDirectory;
        var port = 0;
        var openBrowser = true;
        var showHelp = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--root":
                    rootPath = ReadValue(arguments, ref index, "--root");
                    break;
                case "--port":
                    var portText = ReadValue(arguments, ref index, "--port");
                    if (!int.TryParse(portText, out port) || port is < 0 or > 65535)
                    {
                        throw new ArgumentException("--port must be a number from 0 through 65535.");
                    }
                    break;
                case "--no-open":
                    openBrowser = false;
                    break;
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {arguments[index]}");
            }
        }

        var fullRootPath = Path.IsPathRooted(rootPath)
            ? Path.GetFullPath(rootPath)
            : Path.GetFullPath(rootPath, workingDirectory);

        return new SecretWorkbenchOptions(fullRootPath, port, openBrowser, showHelp);
    }

    private static string ReadValue(IReadOnlyList<string> arguments, ref int index, string option)
    {
        if (++index >= arguments.Count || arguments[index].StartsWith('-'))
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        return arguments[index];
    }
}
