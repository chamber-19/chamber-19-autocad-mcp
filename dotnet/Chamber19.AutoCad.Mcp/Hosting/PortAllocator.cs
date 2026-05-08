using System.Net;
using System.Net.Sockets;

namespace Chamber19.AutoCad.Mcp.Hosting;

internal static class PortAllocator
{
    public const int RangeStart = 5001;
    public const int RangeEnd = 5050;

    public static int? FindFreePort(IPAddress bindAddress)
    {
        for (var port = RangeStart; port <= RangeEnd; port++)
        {
            TcpListener? probe = null;
            try
            {
                probe = new TcpListener(bindAddress, port);
                probe.Start();
                return port;
            }
            catch (SocketException)
            {
                // Port in use; try next.
            }
            finally
            {
                probe?.Stop();
            }
        }

        return null;
    }
}
