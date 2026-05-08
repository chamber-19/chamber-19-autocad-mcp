using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Chamber19.AutoCad.Mcp.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="Backpressure.UseBackpressure"/> using stub depth/capacity getters so
/// the middleware behavior is verified independently of any real
/// <c>AutoCadThreadDispatcher</c> state.
/// </summary>
public sealed class BackpressureTests
{
    private static async Task<(IHost host, HttpClient client)> CreateHostAsync(
        int depth, int capacity)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.Configure(app =>
                {
                    app.UseBackpressure(() => depth, () => capacity);
                    app.Run(async ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status200OK;
                        await ctx.Response.WriteAsync("downstream", ctx.RequestAborted);
                    });
                });
            })
            .StartAsync();
        return (host, host.GetTestClient());
    }

    [Fact]
    public async Task DepthBelowCapacity_PassesThroughTo200()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateHostAsync(depth: 0, capacity: 32);
        using (host)
        {
            var response = await client.PostAsync("/", new StringContent("{}"), ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("downstream", await response.Content.ReadAsStringAsync(ct));
        }
    }

    [Fact]
    public async Task DepthAtCapacity_Returns429_WithRetryAfterAndJsonBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateHostAsync(depth: 5, capacity: 5);
        using (host)
        {
            var response = await client.PostAsync("/", new StringContent("{}"), ct);

            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString() ?? string.Empty);

            var retryAfter = response.Headers.RetryAfter;
            Assert.NotNull(retryAfter);
            Assert.Equal("1", retryAfter!.Delta?.TotalSeconds.ToString());

            var body = await response.Content.ReadAsStringAsync(ct);
            Assert.Contains("\"error\":\"queue_full\"", body);
            Assert.Contains("\"queueDepth\":5", body);
            Assert.Contains("\"queueCapacity\":5", body);
        }
    }

    [Fact]
    public async Task DepthAboveCapacity_Returns429()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateHostAsync(depth: 100, capacity: 32);
        using (host)
        {
            var response = await client.PostAsync("/", new StringContent("{}"), ct);

            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(ct);
            Assert.Contains("\"queueDepth\":100", body);
            Assert.Contains("\"queueCapacity\":32", body);
        }
    }

    [Fact]
    public async Task ZeroCapacity_DoesNotEnforce_PassesThrough()
    {
        // Capacity 0 is treated as "not configured" rather than "block everything" — this lets
        // tests or experimental setups disable backpressure without restructuring.
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateHostAsync(depth: 0, capacity: 0);
        using (host)
        {
            var response = await client.PostAsync("/", new StringContent("{}"), ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
