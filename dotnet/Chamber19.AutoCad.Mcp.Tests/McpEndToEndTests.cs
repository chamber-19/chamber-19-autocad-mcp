using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chamber19.AutoCad.Mcp.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Minimal MCP tool registered only in the end-to-end test host.
/// Returns a fixed JSON payload; does not touch AutoCAD.
/// </summary>
[McpServerToolType]
public static class McpTestPingTool
{
    [McpServerTool(Name = "test_ping")]
    [Description("End-to-end test tool. Returns a fixed payload without touching AutoCAD.")]
    public static string Ping() => "{\"test\":true}";
}

/// <summary>
/// End-to-end HTTP tests for the full middleware pipeline:
///   BearerAuth → Backpressure → MapMcp
/// with a real MCP tool registration in the test assembly.
///
/// These tests differ from <see cref="BearerAuthTests"/> and
/// <see cref="BackpressureTests"/> which use a stub downstream handler.
/// Here, the downstream is the real MCP HTTP routing, proving that
/// authenticated requests flow correctly through the full server stack
/// and that the registered test tool executes via HTTP.
/// </summary>
public sealed class McpEndToEndTests
{
    private const string ValidToken = "e2e-test-token-44-chars-padded-xxxxxxxxxxxx==";

    // MCP endpoint is explicitly mounted at /mcp (SDK 1.3+ default is "" i.e. root).
    private const string McpPath = "/mcp";

    private static async Task<(IHost host, HttpClient client)> CreateMcpHostAsync(
        int queueDepth = 0,
        int queueCapacity = 32,
        CancellationToken ct = default)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithToolsFromAssembly(typeof(McpTestPingTool).Assembly);

        var app = builder.Build();
        app.UseBearerAuth(ValidToken);
        app.UseBackpressure(() => queueDepth, () => queueCapacity);
        app.MapMcp("/mcp");

        await app.StartAsync(ct);
        return (app, app.GetTestClient());
    }

    /// <summary>
    /// Builds a POST request to the MCP endpoint with Accept headers for
    /// both JSON and SSE, matching what a real MCP client would send.
    /// </summary>
    private static HttpRequestMessage McpPost(string method, string paramsJson = "{}")
    {
        var body = $"{{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"{method}\",\"params\":{paramsJson}}}";
        var request = new HttpRequestMessage(HttpMethod.Post, McpPath)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    /// <summary>
    /// Missing bearer token is rejected by BearerAuth before the request
    /// reaches the MCP routing layer.
    /// </summary>
    [Fact]
    public async Task NoAuth_Returns401_AtMcpEndpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateMcpHostAsync(ct: ct);
        using (host)
        {
            var response = await client.SendAsync(McpPost("tools/list"), ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            var auth = response.Headers.WwwAuthenticate.Single();
            Assert.Equal("Bearer", auth.Scheme);
            Assert.Equal("realm=\"chamber19-autocad-mcp\"", auth.Parameter);
        }
    }

    /// <summary>
    /// Wrong bearer token is rejected by BearerAuth with the invalid_token
    /// error code, even when the request targets a real MCP endpoint.
    /// </summary>
    [Fact]
    public async Task WrongToken_Returns401_WithInvalidTokenError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateMcpHostAsync(ct: ct);
        using (host)
        {
            var request = McpPost("tools/list");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-the-right-token");

            var response = await client.SendAsync(request, ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(ct);
            Assert.Contains("\"error\":\"invalid_token\"", body);
        }
    }

    /// <summary>
    /// When the dispatcher queue is at capacity, Backpressure fires after
    /// BearerAuth (auth passes) but before the request reaches MCP routing.
    /// </summary>
    [Fact]
    public async Task ValidToken_QueueFull_Returns429_BeforeMcpRouting()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateMcpHostAsync(queueDepth: 8, queueCapacity: 8, ct: ct);
        using (host)
        {
            var request = McpPost("tools/list");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ValidToken);

            var response = await client.SendAsync(request, ct);

            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(ct);
            Assert.Contains("\"error\":\"queue_full\"", body);
            Assert.Contains("\"queueDepth\":8", body);
        }
    }

    /// <summary>
    /// A valid MCP <c>initialize</c> request with the correct bearer token
    /// reaches the MCP routing layer and receives a well-formed JSON-RPC
    /// result containing server capability information.
    /// </summary>
    [Fact]
    public async Task ValidToken_Initialize_Returns200WithMcpResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateMcpHostAsync(ct: ct);
        using (host)
        {
            const string initParams =
                "{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"test\",\"version\":\"0.1\"}}";

            var request = McpPost("initialize", initParams);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ValidToken);

            var response = await client.SendAsync(request, ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(ct);
            // A successful JSON-RPC response contains "result"; failure would contain "error".
            Assert.Contains("\"result\"", body);
            Assert.Contains("protocolVersion", body);
        }
    }

    /// <summary>
    /// A <c>tools/list</c> request with the correct bearer token returns
    /// the list of registered MCP tools, which must include the test tool
    /// registered from the test assembly.
    /// </summary>
    [Fact]
    public async Task ValidToken_ToolsList_ContainsTestPingTool()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateMcpHostAsync(ct: ct);
        using (host)
        {
            var request = McpPost("tools/list");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ValidToken);

            var response = await client.SendAsync(request, ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(ct);
            Assert.Contains("test_ping", body);
        }
    }

    /// <summary>
    /// A <c>tools/call</c> request for the registered test tool executes
    /// through the full HTTP stack (auth → backpressure → MCP routing →
    /// tool execution) and returns an MCP content result.
    /// </summary>
    [Fact]
    public async Task ValidToken_ToolCall_Returns200WithContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateMcpHostAsync(ct: ct);
        using (host)
        {
            const string callParams = "{\"name\":\"test_ping\",\"arguments\":{}}";
            var request = McpPost("tools/call", callParams);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ValidToken);

            var response = await client.SendAsync(request, ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(ct);
            // Successful tool call: JSON-RPC result wraps MCP text content.
            Assert.Contains("\"result\"", body);
            Assert.Contains("\"type\":\"text\"", body);
        }
    }
}
