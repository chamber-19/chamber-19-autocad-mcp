using System.Net;
using System.Net.Sockets;
using Chamber19.AutoCad.Mcp.Hosting;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

public sealed class PortAllocatorTests
{
    [Fact]
    public void FindFreePort_ReturnsPortInConfiguredRange()
    {
        var port = PortAllocator.FindFreePort(IPAddress.Loopback);

        Assert.NotNull(port);
        Assert.InRange(port!.Value, PortAllocator.RangeStart, PortAllocator.RangeEnd);
    }

    [Fact]
    public void FindFreePort_SkipsBoundPort_AndReturnsNextFree()
    {
        // Bind whatever the allocator currently considers free, then ask again. The second call
        // must skip the now-bound port and return a different one.
        var firstPick = PortAllocator.FindFreePort(IPAddress.Loopback);
        Assert.NotNull(firstPick);

        var blocker = new TcpListener(IPAddress.Loopback, firstPick!.Value);
        blocker.Start();
        try
        {
            var secondPick = PortAllocator.FindFreePort(IPAddress.Loopback);

            Assert.NotNull(secondPick);
            Assert.NotEqual(firstPick.Value, secondPick!.Value);
            Assert.InRange(secondPick.Value, PortAllocator.RangeStart, PortAllocator.RangeEnd);
        }
        finally
        {
            blocker.Stop();
        }
    }
}
