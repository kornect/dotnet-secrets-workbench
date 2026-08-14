using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SecretWorkbench.Services;

public static class BrowserLauncher
{
    public static bool TryOpen(string url, TextWriter error)
    {
        try
        {
            Process.Start(CreateStartInfo(url));
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            error.WriteLine($"Could not open the browser automatically: {exception.Message}");
            error.WriteLine($"Open {url} manually.");
            return false;
        }
    }

    internal static ProcessStartInfo CreateStartInfo(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ProcessStartInfo(url) { UseShellExecute = true };
        }

        var startInfo = new ProcessStartInfo(RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open")
        {
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(url);
        return startInfo;
    }
}
