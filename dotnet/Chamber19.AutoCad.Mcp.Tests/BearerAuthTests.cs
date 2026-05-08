using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Chamber19.AutoCad.Mcp.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

public sealed class BearerAuthTests
{
    private const string ValidToken = "valid-test-token-fixed-length-44-chars-padd==";

    private static async Task<(IHost host, HttpClient client)> CreateHostAsync(CancellationToken ct)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.Configure(app =>
                {
                    app.UseBearerAuth(ValidToken);
                    app.Run(async ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status200OK;
                        await ctx.Response.WriteAsync("ok", ct);
                    });
                });
            })
            .StartAsync(ct);
        return (host, host.GetTestClient());
    }

    [Fact]
    public async Task MissingAuthorizationHeader_Returns401_WithRealmOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateHostAsync(ct);
        using (host)
        {
            var response = await client.PostAsync("/", new StringContent("{}"), ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            var auth = response.Headers.WwwAuthenticate.Single();
            Assert.Equal("Bearer", auth.Scheme);
            Assert.Equal("realm=\"chamber19-autocad-mcp\"", auth.Parameter);

            var body = await response.Content.ReadAsStringAsync(ct);
            Assert.Contains("\"error\":\"unauthorized\"", body);
            Assert.Contains("\"error_description\":\"Bearer token required.\"", body);
        }
    }

    [Fact]
    public async Task InvalidToken_Returns401_WithErrorInvalidToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateHostAsync(ct);
        using (host)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");

            var response = await client.PostAsync("/", new StringContent("{}"), ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            var auth = response.Headers.WwwAuthenticate.Single();
            Assert.Equal("Bearer", auth.Scheme);
            Assert.Contains("realm=\"chamber19-autocad-mcp\"", auth.Parameter);
            Assert.Contains("error=\"invalid_token\"", auth.Parameter);
            Assert.Contains("error_description=\"Bearer token is invalid.\"", auth.Parameter);

            var body = await response.Content.ReadAsStringAsync(ct);
            Assert.Contains("\"error\":\"invalid_token\"", body);
            Assert.Contains("\"error_description\":\"Bearer token is invalid.\"", body);
        }
    }

    [Fact]
    public async Task ValidToken_PassesThroughTo200()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateHostAsync(ct);
        using (host)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ValidToken);

            var response = await client.PostAsync("/", new StringContent("{}"), ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("ok", await response.Content.ReadAsStringAsync(ct));
        }
    }

    [Fact]
    public async Task EmptyBearerToken_Returns401_WithRealmOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await CreateHostAsync(ct);
        using (host)
        {
            // "Authorization: Bearer" with no value should be treated as missing, not invalid.
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer ");

            var response = await client.PostAsync("/", new StringContent("{}"), ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var auth = response.Headers.WwwAuthenticate.Single();
            Assert.Equal("realm=\"chamber19-autocad-mcp\"", auth.Parameter);
        }
    }
}
