using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Chamber19.AutoCad.Mcp.Hosting;

internal static class BearerAuth
{
    private const string Realm = "chamber19-autocad-mcp";
    private const string BearerPrefix = "Bearer ";

    public static IApplicationBuilder UseBearerAuth(this IApplicationBuilder app, string expectedToken)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);

        return app.Use(async (context, next) =>
        {
            var presented = ExtractBearer(context);

            if (presented is null)
            {
                await WriteUnauthorized(context, includeError: false, errorDescription: "Bearer token required.");
                return;
            }

            if (!ConstantTimeEquals(Encoding.UTF8.GetBytes(presented), expectedBytes))
            {
                await WriteUnauthorized(context, includeError: true, errorDescription: "Bearer token is invalid.");
                return;
            }

            await next();
        });
    }

    private static string? ExtractBearer(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var values))
        {
            return null;
        }

        foreach (var raw in values)
        {
            if (string.IsNullOrEmpty(raw))
            {
                continue;
            }

            if (raw.StartsWith(BearerPrefix, StringComparison.Ordinal))
            {
                var token = raw[BearerPrefix.Length..].Trim();
                return token.Length == 0 ? null : token;
            }
        }

        return null;
    }

    private static async Task WriteUnauthorized(HttpContext context, bool includeError, string errorDescription)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = includeError
            ? $"Bearer realm=\"{Realm}\", error=\"invalid_token\", error_description=\"{errorDescription}\""
            : $"Bearer realm=\"{Realm}\"";
        context.Response.ContentType = "application/json; charset=utf-8";

        var body = JsonSerializer.Serialize(new
        {
            error = includeError ? "invalid_token" : "unauthorized",
            error_description = errorDescription,
        });
        await context.Response.WriteAsync(body);
    }

    private static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
