using System.Net;
using System.Net.Sockets;

namespace SecretWorkbench.Services;

public static class LoopbackPort
{
    /// <summary>
    /// Checks whether the IPv4 loopback port can be bound, so a busy port is reported in one line
    /// instead of surfacing as a hosting-startup failure. Port 0 always succeeds because the OS
    /// picks a free port. Another process can still take the port before the server binds it, so
    /// callers keep handling <see cref="Microsoft.AspNetCore.Connections.AddressInUseException"/>.
    /// </summary>
    public static bool IsAvailable(int port)
    {
        if (port == 0)
        {
            return true;
        }

        try
        {
            using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            probe.Bind(new IPEndPoint(IPAddress.Loopback, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
