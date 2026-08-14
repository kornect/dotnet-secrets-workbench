using System.Net;
using System.Net.Sockets;
using SecretWorkbench.Services;

namespace SecretWorkbench.Tests;

public sealed class LoopbackPortTests
{
    [Fact]
    public void DynamicPortIsAlwaysAvailable()
    {
        Assert.True(LoopbackPort.IsAvailable(0));
    }

    [Fact]
    public void APortNobodyIsListeningOnIsAvailable()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        Assert.True(LoopbackPort.IsAvailable(port));
    }

    [Fact]
    public void APortAlreadyBoundOnLoopbackIsNotAvailable()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            Assert.False(LoopbackPort.IsAvailable(((IPEndPoint)listener.LocalEndpoint).Port));
        }
        finally
        {
            listener.Stop();
        }
    }
}
